using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class RefundServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IOrderRepository> _mockOrderRepo;
        private readonly Mock<IOrderDetailRepository> _mockOrderDetailRepo;
        private readonly Mock<IShiftRepository> _mockShiftRepo;
        private readonly Mock<ITransactionRepository> _mockTransactionRepo;
        private readonly Mock<ILogger<RefundService>> _mockLogger;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly Mock<IRealtimeService> _mockRealtimeService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IDbTransaction> _mockTransaction;
        private readonly RefundService _refundService;

        public RefundServiceTests()
        {
            _mockOrderRepo = new Mock<IOrderRepository>();
            _mockOrderDetailRepo = new Mock<IOrderDetailRepository>();
            _mockShiftRepo = new Mock<IShiftRepository>();
            _mockTransactionRepo = new Mock<ITransactionRepository>();
            
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockUnitOfWork.Setup(u => u.Orders).Returns(_mockOrderRepo.Object);
            _mockUnitOfWork.Setup(u => u.OrderDetails).Returns(_mockOrderDetailRepo.Object);
            _mockUnitOfWork.Setup(u => u.Shifts).Returns(_mockShiftRepo.Object);
            _mockUnitOfWork.Setup(u => u.Transactions).Returns(_mockTransactionRepo.Object);
            
            _mockLogger = new Mock<ILogger<RefundService>>();
            _mockStorageService = new Mock<IStorageService>();
            _mockRealtimeService = new Mock<IRealtimeService>();
            _mockMapper = new Mock<IMapper>();
            _mockTransaction = new Mock<IDbTransaction>();

            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_mockTransaction.Object);

            _refundService = new RefundService(
                _mockUnitOfWork.Object,
                _mockLogger.Object,
                _mockStorageService.Object,
                _mockRealtimeService.Object,
                _mockMapper.Object);
        }

        private Order CreateTestOrder(Guid orderId, decimal totalAmount, decimal finalAmount)
        {
            return new Order
            {
                Id = orderId,
                RestaurantId = 1,
                TotalAmount = totalAmount,
                FinalAmount = finalAmount,
                Status = OrderStatus.Served,
                OrderDetails = new List<OrderDetail>
                {
                    new OrderDetail { Id = 1, DishId = 101, Quantity = 1, OriginalPrice = 100000, DiscountedPrice = 100000, SubTotal = 100000, RefundedQuantity = 0 },
                    new OrderDetail { Id = 2, DishId = 102, Quantity = 1, OriginalPrice = 150000, DiscountedPrice = 100000, SubTotal = 100000, RefundedQuantity = 0, PromotionAmount = 50000 }
                }
            };
        }

        private void SetupMocks(Order order, bool hasActiveShift = true)
        {
            _mockOrderRepo.Setup(u => u.GetOrderWithDetailsByIdAsync(order.Id))
                .ReturnsAsync(order);

            if (hasActiveShift)
            {
                _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(order.RestaurantId))
                    .ReturnsAsync(new Shift { Id = 1, Status = ShiftStatus.Open });
            }
            else
            {
                _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(order.RestaurantId))
                    .ReturnsAsync((Shift)null);
            }
        }

        #region 1. Promotion Logic Tests

        // Ensures order-level discounts are correctly pro-rated during partial refunds using a payment ratio.
        [Fact]
        public async Task RefundOrderAsync_OrderLevelDiscount_ShouldApplyPaymentRatio()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 200000, 190000);
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto> { new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 1 } }
            };

            await _refundService.RefundOrderAsync(request);

            _mockOrderRepo.Verify(u => u.AddAsync(It.Is<Order>(o => o.FinalAmount == 95000)), Times.Once);
        }

        // Ensures item-level discounts are properly recognized so the discounted subtotal is used for refund calculation.
        [Fact]
        public async Task RefundOrderAsync_ItemLevelDiscount_ShouldUseDiscountedSubTotal()
        {
            var orderId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId, RestaurantId = 1, TotalAmount = 190000, FinalAmount = 190000, Status = OrderStatus.Served,
                OrderDetails = new List<OrderDetail>
                {
                    new OrderDetail { Id = 1, DishId = 101, Quantity = 1, OriginalPrice = 100000, DiscountedPrice = 90000, SubTotal = 90000, RefundedQuantity = 0, PromotionAmount = 10000 },
                    new OrderDetail { Id = 2, DishId = 102, Quantity = 1, OriginalPrice = 100000, DiscountedPrice = 100000, SubTotal = 100000, RefundedQuantity = 0 }
                }
            };
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto> { new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 1 } }
            };

            await _refundService.RefundOrderAsync(request);

            _mockOrderRepo.Verify(u => u.AddAsync(It.Is<Order>(o => o.FinalAmount == 90000)), Times.Once);
        }

        // Ensures both item-level and order-level discounts are applied simultaneously to the final refund amount.
        [Fact]
        public async Task RefundOrderAsync_MixedDiscounts_ShouldApplyBoth()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 200000, 180000);
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto> { new RefundItemDto { OrderDetailId = 2, QuantityToRefund = 1 } }
            };

            await _refundService.RefundOrderAsync(request);

            _mockOrderRepo.Verify(u => u.AddAsync(It.Is<Order>(o => o.FinalAmount == 90000)), Times.Once);
        }

        #endregion

        #region 2. Validations Tests

        // Verifies that a DomainException is thrown if the specified order does not exist.
        [Fact]
        public async Task RefundOrderAsync_OrderNotFound_ShouldThrowDomainException()
        {
            var request = new RefundRequest { OrderId = Guid.NewGuid() };
            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);
            await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ORDER_NOT_FOUND);
        }

        // Verifies that a DomainException is thrown if no active shift exists for the restaurant.
        [Fact]
        public async Task RefundOrderAsync_NoActiveShift_ShouldThrowDomainException()
        {
            var order = CreateTestOrder(Guid.NewGuid(), 10000, 10000);
            SetupMocks(order, hasActiveShift: false);

            var request = new RefundRequest { OrderId = order.Id };
            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);
            await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.RESTAURANT_SHIFT_NOT_OPENED);
        }

        // Verifies that a DomainException is thrown if the order is already cancelled or refunded.
        [Fact]
        public async Task RefundOrderAsync_OrderAlreadyCancelled_ShouldThrowDomainException()
        {
            var order = CreateTestOrder(Guid.NewGuid(), 10000, 10000);
            order.Status = OrderStatus.Cancelled;
            SetupMocks(order);

            var request = new RefundRequest { OrderId = order.Id };
            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);
            await act.Should().ThrowAsync<DomainException>();
        }

        // Verifies that a DomainException is thrown if attempting to refund an unpaid order.
        [Fact]
        public async Task RefundOrderAsync_OrderUnpaid_ShouldThrowDomainException()
        {
            var order = CreateTestOrder(Guid.NewGuid(), 10000, 10000);
            order.Status = OrderStatus.Unpaid;
            SetupMocks(order);

            var request = new RefundRequest { OrderId = order.Id };
            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);
            await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.REFUND_UNPAID_ORDER_NOT_SUPPORTED);
        }

        // Verifies that objective refunds must include an image proof file.
        [Fact]
        public async Task RefundOrderAsync_ObjectiveMissingImage_ShouldThrowDomainException()
        {
            var order = CreateTestOrder(Guid.NewGuid(), 10000, 10000);
            SetupMocks(order);

            var request = new RefundRequest { OrderId = order.Id, RefundType = RefundType.Objective, ImageFile = null };
            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);
            await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.REFUND_OBJECTIVE_PROOF_REQUIRED);
        }

        #endregion

        #region 3. Edge Cases

        // Ensures that the requested refund quantity is capped at the available (not yet refunded) quantity.
        [Fact]
        public async Task RefundOrderAsync_QuantityCapping_ShouldNotExceedAvailable()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 200000, 200000);
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto> { new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 10 } }
            };

            await _refundService.RefundOrderAsync(request);

            _mockOrderRepo.Verify(u => u.AddAsync(It.Is<Order>(o => o.FinalAmount == 100000)), Times.Once);
            order.OrderDetails.First(d => d.Id == 1).RefundedQuantity.Should().Be(1);
        }

        // Ensures that invalid OrderDetail IDs in the refund request are safely ignored during processing.
        [Fact]
        public async Task RefundOrderAsync_ItemMismatch_ShouldSkipInvalidIds()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 200000, 200000);
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto>
                {
                    new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 1 },
                    new RefundItemDto { OrderDetailId = 999, QuantityToRefund = 1 }
                }
            };

            await _refundService.RefundOrderAsync(request);

            _mockOrderRepo.Verify(u => u.AddAsync(It.Is<Order>(o => o.FinalAmount == 100000)), Times.Once);
        }

        #endregion

        #region 4. Side Effects

        // Verifies that a refund transaction record is created when processing a cash-based objective refund.
        [Fact]
        public async Task RefundOrderAsync_CashObjective_ShouldCreateTransaction()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            SetupMocks(order);
            
            _mockTransactionRepo.Setup(u => u.GetPaymentTransactionByOrderIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new Transaction { PaymentMethod = PaymentMethod.Cash });

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.Objective, IsFullRefund = true, ImageFile = mockFile.Object };

            await _refundService.RefundOrderAsync(request);

            _mockTransactionRepo.Verify(u => u.AddAsync(It.Is<Transaction>(t => t.TransactionType == TransactionType.Refund)), Times.Once);
        }

        // Ensures that realtime status notifications are triggered upon a successful refund.
        [Fact]
        public async Task RefundOrderAsync_Successful_ShouldNotifyRealtime()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            SetupMocks(order);

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.StaffError, IsFullRefund = true };

            await _refundService.RefundOrderAsync(request);

            _mockRealtimeService.Verify(r => r.NotifyOrderStatusChanged(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.AtLeastOnce);
        }

        #endregion

        #region 5. ConfirmSystemErrorPayment Tests

        // Tests successful manual confirmation of a system payment failure, including order status update and transaction logging.
        [Fact]
        public async Task ConfirmSystemErrorPaymentAsync_Success_ShouldUpdateOrderAndAddTransaction()
        {
            var orderId = Guid.NewGuid();
            var order = new Order { Id = orderId, Status = OrderStatus.Unpaid, FinalAmount = 100000, OrderCode = 1234, RestaurantId = 1 };
            _mockOrderRepo.Setup(u => u.GetByIdAsync(orderId)).ReturnsAsync(order);
            _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(1))
                .ReturnsAsync(new Shift { Id = 10 });
            _mockTransactionRepo.Setup(u => u.GetTransactionByOrderIdAsync(orderId))
                .ReturnsAsync(new Transaction { OrderId = orderId, PaymentMethod = PaymentMethod.BankTransfer, TransactionType = TransactionType.Payment });

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);

            var request = new ConfirmSystemPaymentRequest
            {
                OrderId = orderId,
                ImageFile = mockFile.Object,
                ResponsibleStaffId = Guid.NewGuid(),
                Note = "Confirmed manually"
            };

            var result = await _refundService.ConfirmSystemErrorPaymentAsync(request);

            result.Should().BeTrue();
            order.Status.Should().Be(OrderStatus.Pending);
            order.RefundType.Should().Be(RefundType.SystemError);
            _mockTransactionRepo.Verify(u => u.Update(It.Is<Transaction>(t =>
                t.OrderId == orderId &&
                t.ShiftId == 10 &&
                t.Status == OrderTransactionStatus.Success &&
                t.TransactionType == TransactionType.Payment)), Times.Once);
            _mockRealtimeService.Verify(r => r.NotifyOrderStatusChanged(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
        }

        // Verifies that ConfirmSystemErrorPaymentAsync throws a DomainException if the order doesn't exist.
        [Fact]
        public async Task ConfirmSystemErrorPaymentAsync_OrderNotFound_ShouldThrowDomainException()
        {
            _mockOrderRepo.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Order)null);

            var request = new ConfirmSystemPaymentRequest { OrderId = Guid.NewGuid() };
            Func<Task> act = async () => await _refundService.ConfirmSystemErrorPaymentAsync(request);
            await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ORDER_NOT_FOUND);
        }

        // Verifies that only unpaid orders can be manually confirmed for system payment failures.
        [Fact]
        public async Task ConfirmSystemErrorPaymentAsync_InvalidStatus_ShouldThrowDomainException()
        {
            var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Served };
            _mockOrderRepo.Setup(u => u.GetByIdAsync(order.Id)).ReturnsAsync(order);

            var request = new ConfirmSystemPaymentRequest { OrderId = order.Id };
            Func<Task> act = async () => await _refundService.ConfirmSystemErrorPaymentAsync(request);
            await act.Should().ThrowAsync<DomainException>().WithMessage("*Chưa thanh toán*");
        }

        // Ensures that the system continues processing successfully even if SignalR notifications fail during confirmation.
        [Fact]
        public async Task ConfirmSystemErrorPaymentAsync_SignalRFailure_ShouldStillReturnTrue()
        {
            var orderId = Guid.NewGuid();
            var order = new Order { Id = orderId, Status = OrderStatus.Unpaid, FinalAmount = 100000, RestaurantId = 1 };
            _mockOrderRepo.Setup(u => u.GetByIdAsync(orderId)).ReturnsAsync(order);
            _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(1))
                .ReturnsAsync(new Shift { Id = 10, Status = ShiftStatus.Open });
            _mockTransactionRepo.Setup(u => u.GetTransactionByOrderIdAsync(orderId))
                .ReturnsAsync(new Transaction { OrderId = orderId, PaymentMethod = PaymentMethod.BankTransfer, TransactionType = TransactionType.Payment });
            _mockRealtimeService.Setup(r => r.NotifyOrderStatusChanged(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("SignalR Down"));

            var request = new ConfirmSystemPaymentRequest { OrderId = orderId };

            var result = await _refundService.ConfirmSystemErrorPaymentAsync(request);

            result.Should().BeTrue();
            _mockLogger.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Lỗi SignalR")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        // Ensures that a database transaction rollback is triggered if an exception occurs during manual confirmation.
        [Fact]
        public async Task ConfirmSystemErrorPaymentAsync_DatabaseFailure_ShouldRollback()
        {
            var orderId = Guid.NewGuid();
            var order = new Order { Id = orderId, Status = OrderStatus.Unpaid, RestaurantId = 1 };
            _mockOrderRepo.Setup(u => u.GetByIdAsync(orderId)).ReturnsAsync(order);
            _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(1))
                .ReturnsAsync(new Shift { Id = 10, Status = ShiftStatus.Open });
            _mockTransactionRepo.Setup(u => u.GetTransactionByOrderIdAsync(orderId))
                .ReturnsAsync(new Transaction { OrderId = orderId, PaymentMethod = PaymentMethod.BankTransfer, TransactionType = TransactionType.Payment });
            _mockUnitOfWork.Setup(u => u.SaveAsync()).ThrowsAsync(new Exception("DB Dead"));

            var request = new ConfirmSystemPaymentRequest { OrderId = orderId };

            Func<Task> act = async () => await _refundService.ConfirmSystemErrorPaymentAsync(request);

            await act.Should().ThrowAsync<Exception>().WithMessage("DB Dead");
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmSystemErrorPaymentAsync_NoActiveShift_ShouldThrowDomainException()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var order = new Order { Id = orderId, Status = OrderStatus.Unpaid, RestaurantId = 1 };
            _mockOrderRepo.Setup(u => u.GetByIdAsync(orderId)).ReturnsAsync(order);

            _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(1))
                .ReturnsAsync((Shift)null);

            var request = new ConfirmSystemPaymentRequest { OrderId = orderId };

            // Act
            Func<Task> act = async () => await _refundService.ConfirmSystemErrorPaymentAsync(request);

            // Assert
            await act.Should().ThrowAsync<DomainException>().WithMessage("*ca làm đang mở*");
        }

        [Fact]
        public async Task ConfirmSystemErrorPaymentAsync_TransactionNotFound_ShouldThrowDomainException()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var order = new Order { Id = orderId, Status = OrderStatus.Unpaid, RestaurantId = 1 };
            _mockOrderRepo.Setup(u => u.GetByIdAsync(orderId)).ReturnsAsync(order);
            _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(1))
                .ReturnsAsync(new Shift { Id = 10 });

            _mockTransactionRepo.Setup(u => u.GetTransactionByOrderIdAsync(orderId))
                .ReturnsAsync((Transaction)null);

            var request = new ConfirmSystemPaymentRequest { OrderId = orderId };

            // Act
            Func<Task> act = async () => await _refundService.ConfirmSystemErrorPaymentAsync(request);

            // Assert
            await act.Should().ThrowAsync<DomainException>().WithMessage("*Giao dịch không tồn tại*");
        }
        #endregion

        #region 6. Complete Logic Coverage (Exceptions & Branches)

        // Verifies that the refund transaction is rolled back upon any database-related failure.
        [Fact]
        public async Task RefundOrderAsync_DatabaseFailure_ShouldRollback()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            SetupMocks(order);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).ThrowsAsync(new Exception("DB Error"));

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.StaffError, IsFullRefund = true };

            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);

            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // Ensures that logical execution continues even if SignalR notifications fail during refund processing.
        [Fact]
        public async Task RefundOrderAsync_SignalRFailure_ShouldLogWarningAndContinue()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            SetupMocks(order);
            _mockRealtimeService.Setup(r => r.NotifyOrderStatusChanged(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("SignalR Failure"));

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.StaffError, IsFullRefund = true };

            await _refundService.RefundOrderAsync(request);

            _mockLogger.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Lỗi SignalR")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.AtLeastOnce);
        }

        // Ensures that storage service failures during image upload are caught and re-thrown as DomainExceptions.
        [Fact]
        public async Task RefundOrderAsync_ImageUploadFailure_ShouldThrowDomainException()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            order.OrderCode = 5555;
            SetupMocks(order);
            
            _mockStorageService.Setup(s => s.UploadPaymentProofAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Upload Failed"));

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.FileName).Returns("test.jpg");

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.Objective, ImageFile = mockFile.Object, IsFullRefund = true };

            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);

            await act.Should().ThrowAsync<DomainException>().WithMessage("*Lỗi khi tải ảnh minh chứng lên*");
        }

        // Verifies that individual invalid refund items are skipped while valid ones are processed.
        [Fact]
        public async Task RefundOrderAsync_InvalidItemData_ShouldSkip()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 200000, 200000);
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto>
                {
                    new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 1 },
                    new RefundItemDto { OrderDetailId = 0, QuantityToRefund = 1 },
                    new RefundItemDto { OrderDetailId = 1, QuantityToRefund = -5 }
                }
            };

            await _refundService.RefundOrderAsync(request);

            _mockOrderRepo.Verify(u => u.AddAsync(It.Is<Order>(o => o.FinalAmount == 100000)), Times.Once);
        }

        // Verifies that partial refunds must include at least one item list.
        [Fact]
        public async Task RefundOrderAsync_PartialRefundNoItems_ShouldThrowDomainException()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            SetupMocks(order);

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.StaffError, IsFullRefund = false, RefundItems = new List<RefundItemDto>() };

            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);

            await act.Should().ThrowAsync<DomainException>().WithMessage("*Với trường hợp hoàn tiền một phần*");
        }

        // Ensures that refunds for non-cash payments (e.g., Bank Transfer) do not generate a Cash Transaction record.
        [Fact]
        public async Task RefundOrderAsync_NonCashPayment_ShouldNotLogTransaction()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            SetupMocks(order);
            _mockTransactionRepo.Setup(t => t.GetPaymentTransactionByOrderIdAsync(orderId))
                .ReturnsAsync(new Transaction { PaymentMethod = PaymentMethod.BankTransfer });

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.Objective, IsFullRefund = true };
            request.RefundType = RefundType.Objective; 
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            request.ImageFile = mockFile.Object;

            await _refundService.RefundOrderAsync(request);

            _mockTransactionRepo.Verify(u => u.AddAsync(It.IsAny<Transaction>()), Times.Never);
        }

        // Ensures kitchen notifications are skipped if the order details cannot be retrieved.
        [Fact]
        public async Task ConfirmSystemErrorPaymentAsync_OrderWithDetailsNull_ShouldSkipKitchenNotification()
        {
            var orderId = Guid.NewGuid();
            var order = new Order { Id = orderId, Status = OrderStatus.Unpaid, RestaurantId = 1 };
            _mockOrderRepo.Setup(u => u.GetByIdAsync(orderId)).ReturnsAsync(order);
            _mockOrderRepo.Setup(u => u.GetOrderWithDetailsForKdsAsync(orderId)).ReturnsAsync((Order)null);
            _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(1))
                .ReturnsAsync(new Shift { Id = 10, Status = ShiftStatus.Open });
            _mockTransactionRepo.Setup(u => u.GetTransactionByOrderIdAsync(orderId))
                .ReturnsAsync(new Transaction { OrderId = orderId, PaymentMethod = PaymentMethod.BankTransfer, TransactionType = TransactionType.Payment });

            var request = new ConfirmSystemPaymentRequest { OrderId = orderId };

            await _refundService.ConfirmSystemErrorPaymentAsync(request);

            _mockRealtimeService.Verify(r => r.SendOrderToKitchen(It.IsAny<string>(), It.IsAny<OrderRealtimeDto>()), Times.Never);
        }

        // Ensures refund logging is skipped if the original payment transaction is missing.
        [Fact]
        public async Task RefundOrderAsync_CashObjective_OriginalTransactionNull_ShouldNotLogTransaction()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            SetupMocks(order);
            
            _mockTransactionRepo.Setup(t => t.GetPaymentTransactionByOrderIdAsync(orderId))
                .ReturnsAsync((Transaction)null);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.Objective, IsFullRefund = true, ImageFile = mockFile.Object };

            await _refundService.RefundOrderAsync(request);

            _mockTransactionRepo.Verify(u => u.AddAsync(It.Is<Transaction>(t => t.TransactionType == TransactionType.Refund)), Times.Never);
        }

        // Ensures that a refund transaction can still be recorded even if an active shift cannot be found at that moment.
        [Fact]
        public async Task RefundOrderAsync_CashObjective_ShouldUseInitialActiveShiftForTransaction()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            
            _mockOrderRepo.Setup(u => u.GetOrderWithDetailsByIdAsync(orderId)).ReturnsAsync(order);
            
            // Shift is found during validation
            var activeShift = new Shift { Id = 1, Status = ShiftStatus.Open };
            _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(1))
                .ReturnsAsync(activeShift);

            _mockTransactionRepo.Setup(u => u.GetPaymentTransactionByOrderIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new Transaction { PaymentMethod = PaymentMethod.Cash });

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.Objective, IsFullRefund = true, ImageFile = mockFile.Object };

            await _refundService.RefundOrderAsync(request);

            // Verify that we use the shift found at the beginning (Optimization)
            _mockTransactionRepo.Verify(u => u.AddAsync(It.Is<Transaction>(t => 
                t.TransactionType == TransactionType.Refund && 
                t.ShiftId == 1)), Times.Once);
        }

        // Verifies that a DomainException is thrown if no valid items remain after filtering the refund request.
        [Fact]
        public async Task RefundOrderAsync_AllItemsInvalidAfterFiltering_ShouldThrowDomainException()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto>
                {
                    new RefundItemDto { OrderDetailId = 999, QuantityToRefund = 1 }
                }
            };

            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);

            await act.Should().ThrowAsync<DomainException>().WithMessage("*Không tìm thấy món ăn hợp lệ trong đơn hàng gốc*");
        }

        // Verifies that order details are mapped and sent to the kitchen correctly upon confirming a system payment error.
        [Fact]
        public async Task ConfirmSystemErrorPaymentAsync_OrderWithDetailsNotNull_ShouldSendToKitchen()
        {
            var orderId = Guid.NewGuid();
            var order = new Order { Id = orderId, Status = OrderStatus.Unpaid, RestaurantId = 1, OrderCode = 1234, FinalAmount = 50000 };
            _mockOrderRepo.Setup(u => u.GetByIdAsync(orderId)).ReturnsAsync(order);
            _mockShiftRepo.Setup(u => u.GetActiveCashierShiftAsync(1))
                .ReturnsAsync(new Shift { Id = 10, Status = ShiftStatus.Open });
            _mockTransactionRepo.Setup(u => u.GetTransactionByOrderIdAsync(orderId))
                .ReturnsAsync(new Transaction { OrderId = orderId, PaymentMethod = PaymentMethod.BankTransfer, TransactionType = TransactionType.Payment });
            
            var orderDetails = new Order { Id = orderId, OrderCode = 1234 };
            _mockOrderRepo.Setup(u => u.GetOrderWithDetailsForKdsAsync(orderId)).ReturnsAsync(orderDetails);
            
            _mockMapper.Setup(m => m.Map<OrderRealtimeDto>(It.IsAny<Order>())).Returns(new OrderRealtimeDto());

            var request = new ConfirmSystemPaymentRequest { OrderId = orderId };

            await _refundService.ConfirmSystemErrorPaymentAsync(request);

            _mockRealtimeService.Verify(r => r.SendOrderToKitchen("1", It.IsAny<OrderRealtimeDto>()), Times.Once);
        }

        // Verifies that a Null RefundItems list in a partial refund request triggers a DomainException.
        [Fact]
        public async Task RefundOrderAsync_RefundItemsNull_PartialRefund_ShouldThrowDomainException()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 100000);
            SetupMocks(order);

            var request = new RefundRequest 
            { 
                OrderId = orderId, 
                RefundType = RefundType.StaffError, 
                IsFullRefund = false, 
                RefundItems = null! 
            };

            Func<Task> act = async () => await _refundService.RefundOrderAsync(request);

            await act.Should().ThrowAsync<DomainException>().WithMessage("*Với trường hợp hoàn tiền một phần*");
        }

        // Validates that orders with a zero total amount are handled gracefully by falling back to a ratio of 1.
        [Fact]
        public async Task RefundOrderAsync_TotalAmountZero_ShouldUseRatioOne()
        {
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 0, 0);
            SetupMocks(order);

            var request = new RefundRequest { OrderId = orderId, RefundType = RefundType.StaffError, IsFullRefund = true };

            await _refundService.RefundOrderAsync(request);

            _mockOrderRepo.Verify(u => u.AddAsync(It.Is<Order>(o => o.FinalAmount == 0)), Times.Once);
        }

        #endregion

        #region 7. Branch & Corner Case Coverage (Fixing Red/Yellow lines)
        [Fact]
        public async Task RefundOrderAsync_RefundAmountExceedsFinalAmount_ShouldSetFinalToZero()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 100000, 50000);
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto> { new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 1 } }
            };

            // Act
            await _refundService.RefundOrderAsync(request);

            // Assert
            order.FinalAmount.Should().Be(0);
            _mockOrderRepo.Verify(u => u.Update(It.Is<Order>(o => o.FinalAmount == 0)), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefundOrderAsync_RoundingCanMakeRefundExceedFinal_ClampsNewFinalToZero()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 200000, 1000);
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto>
                {
                    new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 1 },
                    new RefundItemDto { OrderDetailId = 2, QuantityToRefund = 1 }
                }
            };

            // Act
            await _refundService.RefundOrderAsync(request);

            // Assert
            order.FinalAmount.Should().Be(0);
            _mockOrderRepo.Verify(u => u.Update(It.Is<Order>(o => o.FinalAmount == 0)), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefundOrderAsync_PartialRefundAllItems_ShouldAutoCancelOrder()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder(orderId, 250000, 250000);
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto>
                {
                    new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 1 },
                    new RefundItemDto { OrderDetailId = 2, QuantityToRefund = 1 }
                }
            };

            // Act
            await _refundService.RefundOrderAsync(request);

            // Assert
            order.Status.Should().Be(OrderStatus.Cancelled);
            _mockOrderRepo.Verify(u => u.Update(It.Is<Order>(o => o.Status == OrderStatus.Cancelled)), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefundOrderAsync_PartialRefund_ExactlyAllItems_ShouldAutoCancel()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId,
                Status = OrderStatus.Served,
                FinalAmount = 100000,
                TotalAmount = 100000,
                OrderDetails = new List<OrderDetail>
                {
                    new OrderDetail { Id = 1, Quantity = 2, RefundedQuantity = 0, SubTotal = 100000, DiscountedPrice = 50000 }
                }
            };
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false, 
                RefundItems = new List<RefundItemDto> { new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 2 } } 
            };

            // Act
            await _refundService.RefundOrderAsync(request);

            // Assert
            order.Status.Should().Be(OrderStatus.Cancelled);
            _mockOrderRepo.Verify(u => u.Update(It.Is<Order>(o => o.Status == OrderStatus.Cancelled)), Times.AtLeastOnce);
        }
        [Fact]
        public async Task RefundOrderAsync_OneThirdRatio_ShouldBeExactlyOneThirdAmount()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId,
                RestaurantId = 1,
                TotalAmount = 90000,
                FinalAmount = 90000,
                Status = OrderStatus.Served,
                OrderDetails = new List<OrderDetail>
                {
                    new OrderDetail { Id = 1, Quantity = 3, RefundedQuantity = 0, SubTotal = 90000, DiscountedPrice = 30000 }
                }
            };
            SetupMocks(order);

            var request = new RefundRequest
            {
                OrderId = orderId,
                RefundType = RefundType.StaffError,
                IsFullRefund = false,
                RefundItems = new List<RefundItemDto> { new RefundItemDto { OrderDetailId = 1, QuantityToRefund = 1 } }
            };

            // Act
            await _refundService.RefundOrderAsync(request);

            // Assert
            // (90000 * 1 / 3) = 30000.
            _mockOrderRepo.Verify(u => u.AddAsync(It.Is<Order>(o => o.FinalAmount == 30000)), Times.Once);
            order.FinalAmount.Should().Be(60000); // 90000 - 30000
        }
        #endregion

    }
}
