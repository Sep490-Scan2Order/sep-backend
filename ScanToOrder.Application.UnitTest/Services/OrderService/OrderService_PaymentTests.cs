using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;
using Xunit;
using AutoMapper;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ScanToOrder.Application.UnitTest.Services.OrderService;

public class OrderService_PaymentTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ICartRedisService> _mockCartRedisService;
    private readonly Mock<ITransactionRedisService> _mockTransactionRedisService;
    private readonly Mock<IRealtimeService> _mockRealtimeService;
    private readonly Mock<IMenuCacheService> _mockMenuCacheService;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IAuthenticatedUserService> _mockAuthUserService;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<ILogger<Application.Services.OrderService>> _mockLogger;
    private readonly Mock<IQrCodeService> _mockQrCodeService;
    private readonly Mock<IPlanLimitationService> _mockPlanLimitationService;
    private readonly Mock<IAIUpsellService> _mockAiUpsellService;

    private readonly Application.Services.OrderService _orderService;

    public OrderService_PaymentTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCartRedisService = new Mock<ICartRedisService>();
        _mockTransactionRedisService = new Mock<ITransactionRedisService>();
        _mockRealtimeService = new Mock<IRealtimeService>();
        _mockMenuCacheService = new Mock<IMenuCacheService>();
        _mockMapper = new Mock<IMapper>();
        _mockAuthUserService = new Mock<IAuthenticatedUserService>();
        _mockStorageService = new Mock<IStorageService>();
        _mockLogger = new Mock<ILogger<Application.Services.OrderService>>();
        _mockQrCodeService = new Mock<IQrCodeService>();
        _mockPlanLimitationService = new Mock<IPlanLimitationService>();
        _mockAiUpsellService = new Mock<IAIUpsellService>();

        _orderService = new Application.Services.OrderService(
            _mockUnitOfWork.Object,
            _mockCartRedisService.Object,
            _mockTransactionRedisService.Object,
            _mockRealtimeService.Object,
            _mockMenuCacheService.Object,
            _mockMapper.Object,
            _mockAuthUserService.Object,
            _mockStorageService.Object,
            _mockLogger.Object,
            _mockQrCodeService.Object,
            _mockPlanLimitationService.Object,
            _mockAiUpsellService.Object,
            new Mock<IBackgroundJobService>().Object
        );
    }

    private void SetupMocks(Order order, Staff staff, Transaction transaction, Shift shift = null)
    {
        if (order != null)
            _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(order.Id)).ReturnsAsync(order);
        
        if (staff != null)
        {
            _mockAuthUserService.Setup(a => a.ProfileId).Returns(staff.Id);
            _mockUnitOfWork.Setup(u => u.Staffs.GetByIdAsync(staff.Id)).ReturnsAsync(staff);
        }

        if (transaction != null)
            _mockUnitOfWork.Setup(u => u.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync(transaction);

        if (shift != null)
        {
            _mockUnitOfWork.Setup(u => u.Shifts.GetByIdAsync(shift.Id)).ReturnsAsync(shift);
            _mockUnitOfWork.Setup(u => u.Shifts.GetActiveCashierShiftAsync(It.IsAny<int>())).ReturnsAsync(shift);
            _mockUnitOfWork.Setup(u => u.Shifts.FirstOrDefaultAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync(shift);
        }

        var mockTx = new Mock<IDbTransaction>();
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
    }

    #region Basic Validations
    [Fact]
    public async Task ConfirmCashPaymentAsync_WithEmptyOrderId_ThrowsDomainException()
    {
        // Act
        var action = async () => await _orderService.ConfirmCashPaymentAsync(Guid.Empty);

        // Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.INVALID_ORDER_ID);
    }

    [Fact]
    public async Task ConfirmCashPaymentAsync_WhenOrderNotFound_ThrowsDomainException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(orderId)).ReturnsAsync((Order)null);

        // Act
        var action = async () => await _orderService.ConfirmCashPaymentAsync(orderId);

        // Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ORDER_NOT_FOUND);
    }

    [Fact]
    public async Task ConfirmCashPaymentAsync_WhenOrderNotUnpaid_ReturnsImmediately()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Pending };
        SetupMocks(order, null, null);

        // Act
        await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Never);
    }
    #endregion

    #region Authentication & Authorization
    [Fact]
    public async Task ConfirmCashPaymentAsync_WhenStaffNotIdentified_ThrowsDomainException()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Unpaid };
        SetupMocks(order, null, null);
        _mockAuthUserService.Setup(a => a.ProfileId).Returns((Guid?)null);

        // Act
        var action = async () => await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.STAFF_NOT_IDENTIFIED);
    }

    [Fact]
    public async Task ConfirmCashPaymentAsync_WhenStaffNotFound_ThrowsDomainException()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Unpaid };
        var staffId = Guid.NewGuid();
        SetupMocks(order, null, null);
        _mockAuthUserService.Setup(a => a.ProfileId).Returns(staffId);
        _mockUnitOfWork.Setup(u => u.Staffs.GetByIdAsync(staffId)).ReturnsAsync((Staff)null);

        // Act
        var action = async () => await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(StaffMessage.StaffError.STAFF_NOT_FOUND);
    }

    [Fact]
    public async Task ConfirmCashPaymentAsync_WhenStaffInDifferentRestaurant_ThrowsDomainException()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Unpaid, RestaurantId = 1 };
        var staff = new Staff { Id = Guid.NewGuid(), RestaurantId = 2 };
        SetupMocks(order, staff, null);

        // Act
        var action = async () => await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(StaffMessage.StaffError.STAFF_NOT_IN_RESTAURANT);
    }
    #endregion

    #region Transaction & Shift
    [Fact]
    public async Task ConfirmCashPaymentAsync_WhenCashTransactionNotFound_ThrowsDomainException()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Unpaid, RestaurantId = 1 };
        var staff = new Staff { Id = Guid.NewGuid(), RestaurantId = 1 };
        SetupMocks(order, staff, null);
        _mockUnitOfWork.Setup(u => u.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync((Transaction)null);

        // Act
        var action = async () => await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.CASH_TRANSACTION_NOT_FOUND);
    }

    [Fact]
    public async Task ConfirmCashPaymentAsync_WhenTransactionAlreadySuccess_ReturnsImmediately()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Unpaid, RestaurantId = 1 };
        var staff = new Staff { Id = Guid.NewGuid(), RestaurantId = 1 };
        var transaction = new Transaction { Id = 1, OrderId = order.Id, Status = OrderTransactionStatus.Success };
        SetupMocks(order, staff, transaction);

        // Act
        await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfirmCashPaymentAsync_WhenNoActiveShift_ThrowsDomainException()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Unpaid, RestaurantId = 1 };
        var staff = new Staff { Id = Guid.NewGuid(), RestaurantId = 1 };
        var transaction = new Transaction { Id = 1, OrderId = order.Id, Status = OrderTransactionStatus.Pending };

        SetupMocks(order, staff, transaction);
        _mockUnitOfWork.Setup(u => u.Shifts.GetActiveCashierShiftAsync(order.RestaurantId))
            .ReturnsAsync((Shift)null);

        // Act
        var action = async () => await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(ShiftMessage.ShiftError.SHIFT_NOT_OPEN_YET);
    }

    #endregion

    #region Success Paths
    [Fact]
    public async Task ConfirmCashPaymentAsync_WithExistingShift_UpdatesStatusAndNotifies()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), OrderCode = 101, Status = OrderStatus.Unpaid, RestaurantId = 1 };
        var staff = new Staff { Id = Guid.NewGuid(), RestaurantId = 1 };
        var shift = new Shift { Id = 200, StaffId = staff.Id };
        var transaction = new Transaction { Id = 1, OrderId = order.Id, Status = OrderTransactionStatus.Pending, ShiftId = shift.Id, TotalAmount = 100000 };
        
        SetupMocks(order, staff, transaction, shift);

        // Act
        await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        order.Status.Should().Be(OrderStatus.Pending);
        transaction.Status.Should().Be(OrderTransactionStatus.Success);
        _mockUnitOfWork.Verify(u => u.Orders.Update(order), Times.Once);
        _mockUnitOfWork.Verify(u => u.Transactions.Update(transaction), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        _mockRealtimeService.Verify(r => r.NotifyOrderStatusChanged(It.IsAny<string>(), It.IsAny<string>(), (int)OrderStatus.Pending), Times.Once);
        _mockRealtimeService.Verify(r => r.NotifyPaymentReceived(It.IsAny<string>(), 101, 100000, ""), Times.Once);
    }

    [Fact]
    public async Task ConfirmCashPaymentAsync_WithNewOpenShift_LinksShiftAndUpdatesStatus()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), OrderCode = 102, Status = OrderStatus.Unpaid, RestaurantId = 1 };
        var staff = new Staff { Id = Guid.NewGuid(), RestaurantId = 1 };
        var transaction = new Transaction { Id = 1, OrderId = order.Id, Status = OrderTransactionStatus.Pending, ShiftId = null, TotalAmount = 50000 };
        var activeShift = new Shift { Id = 300, StaffId = staff.Id, Status = ShiftStatus.Open, RestaurantId = 1 };

        SetupMocks(order, staff, transaction, activeShift);

        // Act
        await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        transaction.ShiftId.Should().Be(activeShift.Id);
        order.Status.Should().Be(OrderStatus.Pending);
        transaction.Status.Should().Be(OrderTransactionStatus.Success);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }
    #endregion

    #region Failure Path
    [Fact]
    public async Task ConfirmCashPaymentAsync_OnDbException_RollbacksTransaction()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Unpaid, RestaurantId = 1 };
        var staff = new Staff { Id = Guid.NewGuid(), RestaurantId = 1 };
        var transaction = new Transaction { Id = 1, OrderId = order.Id, Status = OrderTransactionStatus.Pending, PaymentMethod = PaymentMethod.Cash };
        
        SetupMocks(order, staff, transaction);
        _mockUnitOfWork.Setup(u => u.SaveAsync()).ThrowsAsync(new System.Exception("DB Error"));
        
        var mockTx = new Mock<IDbTransaction>();
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        // Act
        var action = async () => await _orderService.ConfirmCashPaymentAsync(order.Id);

        // Assert
        await action.Should().ThrowAsync<System.Exception>().WithMessage("DB Error");
        mockTx.Verify(t => t.RollbackAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }
    #endregion
}
