using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Shift;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.Message;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class ShiftServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IShiftRepository> _mockShiftRepo;
        private readonly Mock<IRestaurantRepository> _mockRestaurantRepo;
        private readonly Mock<ITransactionRepository> _mockTransactionRepo;
        private readonly Mock<IShiftReportRepository> _mockShiftReportRepo;
        private readonly Mock<IStaffRepository> _mockStaffRepo;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IRealtimeService> _mockRealtimeService;
        private readonly Mock<IDbTransaction> _mockTransaction;
        private readonly Mock<IAuthenticatedUserService> _mockAuthenticatedUserService;
        private readonly ShiftService _shiftService;

        public ShiftServiceTests()
        {
            _mockShiftRepo = new Mock<IShiftRepository>();
            _mockRestaurantRepo = new Mock<IRestaurantRepository>();
            _mockTransactionRepo = new Mock<ITransactionRepository>();
            _mockShiftReportRepo = new Mock<IShiftReportRepository>();
            _mockStaffRepo = new Mock<IStaffRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockRealtimeService = new Mock<IRealtimeService>();
            _mockTransaction = new Mock<IDbTransaction>();
            _mockAuthenticatedUserService = new Mock<IAuthenticatedUserService>();

            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUnitOfWork.Setup(u => u.Shifts).Returns(_mockShiftRepo.Object);
            _mockUnitOfWork.Setup(u => u.Restaurants).Returns(_mockRestaurantRepo.Object);
            _mockUnitOfWork.Setup(u => u.Transactions).Returns(_mockTransactionRepo.Object);
            _mockUnitOfWork.Setup(u => u.ShiftReports).Returns(_mockShiftReportRepo.Object);
            _mockUnitOfWork.Setup(u => u.Staffs).Returns(_mockStaffRepo.Object);
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_mockTransaction.Object);

            _mockAuthenticatedUserService.Setup(a => a.Role).Returns("Cashier");

            _shiftService = new ShiftService(_mockUnitOfWork.Object, _mockMapper.Object, _mockRealtimeService.Object, _mockAuthenticatedUserService.Object);
        }

        #region 1. CheckIn Tests

        // Tests successful shift check-in when all prerequisites are met.
        [Fact]
        public async Task CheckInShiftAsync_Success_ShouldCreateShift()
        {
            var staffId = Guid.NewGuid();
            var restaurantId = 1;
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(restaurantId)).ReturnsAsync(new Restaurant { Id = restaurantId, MinCashAmount = 100000, Slug = "test" });
            _mockShiftRepo.Setup(s => s.GetCurrentShiftByStaffIdAsync(staffId)).ReturnsAsync((Shift)null);
            _mockShiftRepo.Setup(s => s.GetActiveCashierShiftAsync(restaurantId)).ReturnsAsync((Shift)null);
            _mockMapper.Setup(m => m.Map<ShiftDto>(It.IsAny<Shift>())).Returns(new ShiftDto());

            var result = await _shiftService.CheckInShiftAsync(restaurantId, staffId, "Morning shift");

            result.Should().NotBeNull();
            _mockShiftRepo.Verify(s => s.AddAsync(It.Is<Shift>(x => x.Status == ShiftStatus.Open)), Times.Once);
            _mockRealtimeService.Verify(r => r.NotifyShiftChanged(staffId.ToString(), It.IsAny<ShiftDto>()), Times.Once);
        }

        [Fact]
        public async Task CheckInShiftAsync_StaffSuccess_ShouldCreateShiftWithParentId()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            var restaurantId = 1;
            var cashierShift = new Shift { Id = 10, Type = ShiftType.Cashier };
            
            _mockAuthenticatedUserService.Setup(a => a.Role).Returns("Staff");
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(restaurantId)).ReturnsAsync(new Restaurant { Slug = "test" });
            _mockShiftRepo.Setup(s => s.GetCurrentShiftByStaffIdAsync(staffId)).ReturnsAsync((Shift)null);
            _mockShiftRepo.Setup(s => s.GetActiveCashierShiftAsync(restaurantId)).ReturnsAsync(cashierShift);
            _mockMapper.Setup(m => m.Map<ShiftDto>(It.IsAny<Shift>())).Returns(new ShiftDto());

            // Act
            var result = await _shiftService.CheckInShiftAsync(restaurantId, staffId, "Staff shift");

            // Assert
            result.Should().NotBeNull();
            _mockShiftRepo.Verify(s => s.AddAsync(It.Is<Shift>(x => 
                x.Type == ShiftType.Staff && 
                x.ParentShiftId == 10)), Times.Once);
        }

        [Fact]
        public async Task CheckInShiftAsync_StaffFailure_NoCashierShift_ShouldThrowDomainException()
        {
            // Arrange
            _mockAuthenticatedUserService.Setup(a => a.Role).Returns("Staff");
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Slug = "test" });
            _mockShiftRepo.Setup(s => s.GetCurrentShiftByStaffIdAsync(It.IsAny<Guid>())).ReturnsAsync((Shift)null);
            _mockShiftRepo.Setup(s => s.GetActiveCashierShiftAsync(1)).ReturnsAsync((Shift)null);

            // Act
            Func<Task> act = async () => await _shiftService.CheckInShiftAsync(1, Guid.NewGuid(), null);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(ShiftMessage.ShiftError.CASHIER_SHIFT_NOT_OPEN);
        }

        [Fact]
        public async Task CheckInShiftAsync_AlreadyHasOpenShift_ShouldThrowDomainException()
        {
            // Arrange
            var staffId = Guid.NewGuid();
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Slug = "test" });
            _mockShiftRepo.Setup(s => s.GetCurrentShiftByStaffIdAsync(staffId)).ReturnsAsync(new Shift());

            // Act
            Func<Task> act = async () => await _shiftService.CheckInShiftAsync(1, staffId, null);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(ShiftMessage.ShiftError.SHIFT_ALREADY_OPEN);
        }

        [Fact]
        public async Task CheckInShiftAsync_NullNote_ShouldCreateShiftWithEmptyNote()
        {
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Restaurant { Slug = "test" });
            _mockShiftRepo.Setup(s => s.GetCurrentShiftByStaffIdAsync(It.IsAny<Guid>())).ReturnsAsync((Shift)null);
            _mockShiftRepo.Setup(s => s.GetActiveCashierShiftAsync(It.IsAny<int>())).ReturnsAsync((Shift)null);

            await _shiftService.CheckInShiftAsync(1, Guid.NewGuid(), null);

            _mockShiftRepo.Verify(s => s.AddAsync(It.Is<Shift>(x => x.Note == string.Empty)), Times.Once);
        }

        // Verifies that CheckInShiftAsync throws a DomainException if the restaurant is not found.
        [Fact]
        public async Task CheckInShiftAsync_RestaurantNotFound_ShouldThrowDomainException()
        {
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Restaurant)null);

            Func<Task> act = async () => await _shiftService.CheckInShiftAsync(1, Guid.NewGuid(), null);

            await act.Should().ThrowAsync<DomainException>();
        }

        // Verifies that check-in fails if the opening cash is below the restaurant's minimum required amount.
        [Fact]
        public async Task CheckInShiftAsync_OpeningCashInvalid_ShouldThrowDomainException()
        {
            var restaurant = new Restaurant { Id = 1, MinCashAmount = 100000, Slug = "test" };
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(restaurant);

            Func<Task> act = async () => await _shiftService.CheckInShiftAsync(1, Guid.NewGuid(), null);

            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task CheckInShiftAsync_ShiftAlreadyOpen_ShouldThrowDomainException()
        {
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Id = 1, MinCashAmount = 0, Slug = "test" });
            _mockShiftRepo.Setup(s => s.GetCurrentShiftByStaffIdAsync(It.IsAny<Guid>())).ReturnsAsync((Shift)null);
            _mockShiftRepo.Setup(s => s.GetActiveCashierShiftAsync(1)).ReturnsAsync(new Shift());

            Func<Task> act = async () => await _shiftService.CheckInShiftAsync(1, Guid.NewGuid(), null);

            await act.Should().ThrowAsync<DomainException>();
        }

        #endregion

        #region 2. CheckOut Tests

        // Verifies that CheckOutShiftAsync throws a DomainException when the shift ID is invalid.
        [Fact]
        public async Task CheckOutShiftAsync_ShiftNotFound_ShouldThrowDomainException()
        {
            _mockShiftRepo.Setup(s => s.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Shift)null);
            Func<Task> act = async () => await _shiftService.CheckOutShiftAsync(1, null);
            await act.Should().ThrowAsync<DomainException>();
        }

        // Tests successful shift check-out with accurate calculation of cash, transfers, and refunds.
        [Fact]
        public async Task CheckOutShiftAsync_Success_ShouldCalculateRevenueCorrectly()
        {
            var shiftId = 1;
            var staffId = Guid.NewGuid();
            var shift = new Shift { Id = shiftId, StaffId = staffId, Status = ShiftStatus.Open };
            _mockShiftRepo.Setup(s => s.GetByIdAsync(shiftId)).ReturnsAsync(shift);
            _mockMapper.Setup(m => m.Map<ShiftDto>(It.IsAny<Shift>())).Returns(new ShiftDto());

            var transactions = new List<Transaction>
            {
                new Transaction { PaymentMethod = PaymentMethod.Cash, TransactionType = TransactionType.Payment, Status = OrderTransactionStatus.Success, Order = new Order { Status = OrderStatus.Served, FinalAmount = 200000 } },
                new Transaction { PaymentMethod = PaymentMethod.Cash, TransactionType = TransactionType.Refund, TotalAmount = 50000, Status = OrderTransactionStatus.Success, Order = new Order { Status = OrderStatus.Served, FinalAmount = 0 } },
                new Transaction { PaymentMethod = PaymentMethod.BankTransfer, TransactionType = TransactionType.Payment, Status = OrderTransactionStatus.Success, Order = new Order { Status = OrderStatus.Served, FinalAmount = 300000 } }
            };
            _mockTransactionRepo.Setup(t => t.GetSuccessfulTransactionsByShiftIdAsync(shiftId))
                .ReturnsAsync(transactions);

            // expectedCash = opening(100k) + cashPayment(200k) = 300k
            var result = await _shiftService.CheckOutShiftAsync(shiftId, "Closing shift");

            result.Should().NotBeNull();
            shift.Status.Should().Be(ShiftStatus.Closed);
            _mockShiftReportRepo.Verify(r => r.AddAsync(It.Is<ShiftReport>(x => 
                x.ShiftId == shiftId &&
                x.TotalCashOrder == 200000 && 
                x.TotalTransferOrder == 300000 && 
                x.TotalRefundAmount == 50000 &&
                x.Difference == -200000 &&
                x.Note == "Closing shift")), Times.Once);
        }

        [Fact]
        public async Task CheckOutShiftAsync_NullNote_ShouldCloseShiftWithEmptyNote()
        {
            var shift = new Shift { Id = 1, Status = ShiftStatus.Open };
            _mockShiftRepo.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(shift);
            _mockTransactionRepo.Setup(t => t.GetSuccessfulTransactionsByShiftIdAsync(1))
                .ReturnsAsync(new List<Transaction>());

            await _shiftService.CheckOutShiftAsync(1, null);

            shift.Note.Should().Be(string.Empty);
            _mockShiftReportRepo.Verify(r => r.AddAsync(It.Is<ShiftReport>(x => x.Note == string.Empty)), Times.Once);
        }

        [Fact]
        public async Task CheckOutShiftAsync_Failure_ShouldRollback()
        {
            var shift = new Shift { Id = 1, Status = ShiftStatus.Open, Type = ShiftType.Cashier };
            _mockShiftRepo.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(shift);
            _mockTransactionRepo.Setup(t => t.GetSuccessfulTransactionsByShiftIdAsync(1))
                .ReturnsAsync(new List<Transaction>());
            _mockUnitOfWork.Setup(u => u.SaveAsync()).ThrowsAsync(new Exception("Fail"));

            Func<Task> act = async () => await _shiftService.CheckOutShiftAsync(1, null);

            await act.Should().ThrowAsync<Exception>();
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // Verifies that CheckOutShiftAsync throws a DomainException if attempting to check out a shift that is already closed.
        [Fact]
        public async Task CheckOutShiftAsync_AlreadyClosed_ShouldThrowDomainException()
        {
            var shift = new Shift { Id = 1, Status = ShiftStatus.Closed };
            _mockShiftRepo.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(shift);
            Func<Task> act = async () => await _shiftService.CheckOutShiftAsync(1, null);
            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task CheckOutShiftAsync_ActualCashBelowMinimum_ShouldThrowDomainException()
        {
            // Arrange
            var shiftId = 1;
            var restaurantId = 10;
            var shift = new Shift { Id = shiftId, RestaurantId = restaurantId, Status = ShiftStatus.Open };
            var restaurant = new Restaurant { Id = restaurantId, MinCashAmount = 500000, Slug = "test-store" };

            _mockShiftRepo.Setup(s => s.GetByIdAsync(shiftId)).ReturnsAsync(shift);
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(restaurantId)).ReturnsAsync(restaurant);

            // Act
            Func<Task> act = async () => await _shiftService.CheckOutShiftAsync(shiftId, "Too low cash");

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("*mức tối thiểu*");
        }

        [Fact]
        public async Task CheckOutShiftAsync_StaffSuccess_ShouldNotCreateReport()
        {
            // Arrange
            var shiftId = 1;
            var staffId = Guid.NewGuid();
            var shift = new Shift { Id = shiftId, StaffId = staffId, Type = ShiftType.Staff, Status = ShiftStatus.Open };
            _mockShiftRepo.Setup(s => s.GetByIdAsync(shiftId)).ReturnsAsync(shift);
            _mockMapper.Setup(m => m.Map<ShiftDto>(shift)).Returns(new ShiftDto());

            // Act
            await _shiftService.CheckOutShiftAsync(shiftId, "Done");

            // Assert
            shift.Status.Should().Be(ShiftStatus.Closed);
            _mockShiftReportRepo.Verify(r => r.AddAsync(It.IsAny<ShiftReport>()), Times.Never);
            _mockRealtimeService.Verify(r => r.NotifyShiftChanged(staffId.ToString(), It.IsAny<ShiftDto>()), Times.Once);
        }

        [Fact]
        public async Task CheckOutShiftAsync_CashierFailure_HasOpenSubShifts_ShouldThrowDomainException()
        {
            // Arrange
            var shiftId = 1;
            var restaurantId = 10;
            var shift = new Shift { Id = shiftId, RestaurantId = restaurantId, Type = ShiftType.Cashier, Status = ShiftStatus.Open };
            _mockShiftRepo.Setup(s => s.GetByIdAsync(shiftId)).ReturnsAsync(shift);
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(restaurantId)).ReturnsAsync(new Restaurant { Slug = "test" });
            _mockShiftRepo.Setup(s => s.HasOpenSubShiftsAsync(shiftId)).ReturnsAsync(true);

            // Act
            Func<Task> act = async () => await _shiftService.CheckOutShiftAsync(shiftId, null);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(ShiftMessage.ShiftError.STAFF_MUST_CHECKOUT_FIRST);
        }

        #endregion

        #region 3. Query Tests

        // Ensures shift report details are correctly retrieved and mapped to a DTO for a valid shift.
        [Fact]
        public async Task GetShiftReportAsync_Success_ShouldReturnDto()
        {
            var shiftId = 1;
            var staffId = Guid.NewGuid();
            _mockShiftRepo.Setup(s => s.GetByIdAsync(shiftId)).ReturnsAsync(new Shift { Id = shiftId, StaffId = staffId });
            _mockStaffRepo.Setup(s => s.GetByIdAsync(staffId)).ReturnsAsync(new Staff { Name = "John" });
            _mockShiftReportRepo.Setup(r => r.GetReportByShiftIdAsync(shiftId))
                .ReturnsAsync(new ShiftReport { ShiftId = shiftId, TotalCashOrder = 100000, TotalTransferOrder = 50000 });

            // Mock mapper for tuple to DTO
            _mockMapper.Setup(m => m.Map<ShiftReportDto>(It.IsAny<(ShiftReport, string)>()))
                .Returns((ValueTuple<ShiftReport, string> src) => new ShiftReportDto 
                { 
                    CashierName = src.Item2, 
                    ExpectedTotalAmount = src.Item1.TotalCashOrder + src.Item1.TotalTransferOrder 
                });

            var result = await _shiftService.GetShiftReportAsync(shiftId);

            result.Should().NotBeNull();
            result.CashierName.Should().Be("John");
            result.ExpectedTotalAmount.Should().Be(150000); // 100k cash + 50k bank
        }

        [Fact]
        public async Task GetShiftReportAsync_StaffNotFound_ShouldReturnEmptyCashierName()
        {
            _mockShiftRepo.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(new Shift { Id = 1, StaffId = Guid.NewGuid() });
            _mockStaffRepo.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Staff)null);
            _mockShiftReportRepo.Setup(r => r.GetReportByShiftIdAsync(1))
                .ReturnsAsync(new ShiftReport { ShiftId = 1 });

            // Mock mapper for tuple to DTO
            _mockMapper.Setup(m => m.Map<ShiftReportDto>(It.IsAny<(ShiftReport, string)>()))
                .Returns((ValueTuple<ShiftReport, string> src) => new ShiftReportDto { CashierName = src.Item2 });

            var result = await _shiftService.GetShiftReportAsync(1);

            result.CashierName.Should().Be(string.Empty);
        }

        // Verifies that GetShiftReportAsync throws a DomainException when the requested shift does not exist.
        [Fact]
        public async Task GetShiftReportAsync_ShiftNotFound_ShouldThrowDomainException()
        {
            _mockShiftRepo.Setup(s => s.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Shift)null);
            Func<Task> act = async () => await _shiftService.GetShiftReportAsync(1);
            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task GetShiftReportAsync_ReportNotFound_ShouldThrowDomainException()
        {
            _mockShiftRepo.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(new Shift { Id = 1 });
            _mockShiftReportRepo.Setup(r => r.GetReportByShiftIdAsync(1))
                .ReturnsAsync((ShiftReport)null);

            Func<Task> act = async () => await _shiftService.GetShiftReportAsync(1);
            await act.Should().ThrowAsync<DomainException>();
        }

        // Verifies that current shift retrieval works as expected for a staff member with an active shift.
        [Fact]
        public async Task GetShiftByIdAsync_Success_ShouldReturnDto()
        {
            var staffId = Guid.NewGuid();
            var shift = new Shift { Id = 1, StaffId = staffId };
            _mockShiftRepo.Setup(s => s.GetCurrentShiftByStaffIdAsync(staffId)).ReturnsAsync(shift);
            _mockMapper.Setup(m => m.Map<ShiftDto>(shift)).Returns(new ShiftDto { Id = 1 });

            var result = await _shiftService.GetShiftByIdAsync(staffId);

            result.Should().NotBeNull();
            result.Id.Should().Be(1);
        }

        // Verifies that GetShiftByIdAsync throws a DomainException if no current shift is found for the specified staff member.
        [Fact]
        public async Task GetShiftByIdAsync_NotFound_ShouldThrowDomainException()
        {
            _mockShiftRepo.Setup(s => s.GetCurrentShiftByStaffIdAsync(It.IsAny<Guid>())).ReturnsAsync((Shift)null);
            Func<Task> act = async () => await _shiftService.GetShiftByIdAsync(Guid.NewGuid());
            await act.Should().ThrowAsync<DomainException>();
        }

        // Tests paginated retrieval of shift reports for a specific restaurant, including time-range filtering.
        [Fact]
        public async Task GetAllShiftReportsAsync_Success_ShouldReturnPagedResult()
        {
            var reports = new List<(ShiftReport Report, string CashierName)>
            {
                (new ShiftReport { Id = 1 }, "Admin")
            };

            _mockShiftReportRepo.Setup(r => r.GetReportsByRestaurantAsync(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((reports, 1));

            _mockMapper.Setup(m => m.Map<ShiftReportDto>(It.IsAny<(ShiftReport, string)>()))
                .Returns((ValueTuple<ShiftReport, string> src) => new ShiftReportDto { CashierName = src.Item2 });

            var result = await _shiftService.GetAllShiftReportsAsync(1, 1, 10, null, null);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items.First().CashierName.Should().Be("Admin");
        }

        // Tests paginated retrieval of shift reports for a specific staff member.
        [Fact]
        public async Task GetShiftReportsByStaffAsync_Success_ShouldReturnPagedResult()
        {
            var reports = new List<(ShiftReport Report, string CashierName)>
            {
                (new ShiftReport { Id = 1 }, "Staff")
            };

            _mockShiftReportRepo.Setup(r => r.GetReportsByStaffAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((reports, 1));

            _mockMapper.Setup(m => m.Map<ShiftReportDto>(It.IsAny<(ShiftReport, string)>()))
                .Returns((ValueTuple<ShiftReport, string> src) => new ShiftReportDto { CashierName = src.Item2 });

            var result = await _shiftService.GetShiftReportsByStaffAsync(Guid.NewGuid(), 1, 10);

            result.Should().NotBeNull();
            result.Items.First().CashierName.Should().Be("Staff");
        }

        [Fact]
        public async Task GetStaffShiftsByCashierShiftIdAsync_Success_ReturnsMappedData()
        {
            // Arrange
            var cashierShiftId = 1;
            var subShifts = new List<Shift> { new Shift { Id = 2, ParentShiftId = cashierShiftId } };
            _mockShiftRepo.Setup(s => s.GetOpenSubShiftsByParentIdAsync(cashierShiftId)).ReturnsAsync(subShifts);
            _mockMapper.Setup(m => m.Map<IEnumerable<ShiftDto>>(subShifts)).Returns(new List<ShiftDto> { new ShiftDto { Id = 2 } });

            // Act
            var result = await _shiftService.GetStaffShiftsByCashierShiftIdAsync(cashierShiftId);

            // Assert
            result.Should().HaveCount(1);
            result.First().Id.Should().Be(2);
        }

        #endregion

        #region 4. GetShiftPreview Tests 

        [Fact]
        public async Task GetShiftPreviewAsync_Success_ReturnsCorrectPreview()
        {
            // Arrange
            var shiftId = 1;
            var staffId = Guid.NewGuid();
            var shift = new Shift
            {
                Status = ShiftStatus.Open,
                Note = "Ca sáng"
            };
            var staff = new Staff { Id = staffId, Name = "Đạt D" };
            var transactions = new List<Transaction>
            {
                new Transaction { PaymentMethod = PaymentMethod.Cash, TotalAmount = 50000, Status = OrderTransactionStatus.Success, TransactionType = TransactionType.Payment, Order = new Order { Status = OrderStatus.Served, FinalAmount = 50000 } },
                new Transaction { PaymentMethod = PaymentMethod.BankTransfer, TotalAmount = 150000, Status = OrderTransactionStatus.Success, TransactionType = TransactionType.Payment, Order = new Order { Status = OrderStatus.Served, FinalAmount = 150000 } },
                new Transaction { PaymentMethod = PaymentMethod.Cash, TotalAmount = 100000, Status = OrderTransactionStatus.Success, TransactionType = TransactionType.Payment, Order = new Order { Status = OrderStatus.Preparing, FinalAmount = 100000 } }, 
                new Transaction { PaymentMethod = PaymentMethod.BankTransfer, TotalAmount = 20000, Status = OrderTransactionStatus.Success, TransactionType = TransactionType.Refund, Order = new Order { Status = OrderStatus.Cancelled, FinalAmount = 0 } } 
            };
            
            _mockShiftRepo.Setup(s => s.GetByIdAsync(shiftId)).ReturnsAsync(shift);
            _mockStaffRepo.Setup(s => s.GetByIdAsync(staffId)).ReturnsAsync(staff);
            _mockTransactionRepo.Setup(t => t.GetSuccessfulTransactionsByShiftIdAsync(shiftId))
                .ReturnsAsync(transactions);

            // Act
            var result = await _shiftService.GetShiftPreviewAsync(shiftId);

            // Assert
            result.Should().NotBeNull();
            result.CashierName.Should().Be("Đạt D");
            // Cash: 50k. ExpectedCash = 100k open + 50k = 150k
            result.ExpectedTotalAmount.Should().Be(200000);
            result.Note.Should().Be("Ca sáng");
        }

        [Fact]
        public async Task GetShiftPreviewAsync_WhenStaffAndNoteAreNull_ReturnsEmptyStrings()
        {
            // Arrange
            var shiftId = 1;
            var shift = new Shift { Id = shiftId, StaffId = Guid.NewGuid(), Status = ShiftStatus.Open, Note = null };

            _mockShiftRepo.Setup(s => s.GetByIdAsync(shiftId)).ReturnsAsync(shift);
            _mockStaffRepo.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Staff)null);
            _mockTransactionRepo.Setup(t => t.GetSuccessfulTransactionsByShiftIdAsync(shiftId))
                .ReturnsAsync(new List<Transaction>());

            // Act
            var result = await _shiftService.GetShiftPreviewAsync(shiftId);

            // Assert
            result.CashierName.Should().Be(string.Empty);
            result.Note.Should().Be(string.Empty);
        }

        #endregion

        #region 5. BlockStaffShift Tests

        [Fact]
        public async Task BlockStaffShiftAsync_Success_ShouldBlockShift()
        {
            // Arrange
            var shiftId = 1;
            var staffId = Guid.NewGuid();
            var shift = new Shift { Id = shiftId, StaffId = staffId, Type = ShiftType.Staff, Status = ShiftStatus.Open };
            _mockShiftRepo.Setup(s => s.GetByIdAsync(shiftId)).ReturnsAsync(shift);
            _mockMapper.Setup(m => m.Map<ShiftDto>(shift)).Returns(new ShiftDto { Id = shiftId, Status = ShiftStatus.Blocked });

            // Act
            await _shiftService.BlockStaffShiftAsync(shiftId, "Blocked by admin");

            // Assert
            shift.Status.Should().Be(ShiftStatus.Blocked);
            shift.Note.Should().Be("Blocked by admin");
            _mockShiftRepo.Verify(s => s.Update(shift), Times.Once);
            _mockRealtimeService.Verify(r => r.NotifyShiftChanged(staffId.ToString(), It.IsAny<ShiftDto>()), Times.Once);
        }

        [Fact]
        public async Task BlockStaffShiftAsync_ShiftNotFound_ShouldThrowDomainException()
        {
            // Arrange
            _mockShiftRepo.Setup(s => s.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Shift)null);

            // Act
            Func<Task> act = async () => await _shiftService.BlockStaffShiftAsync(1, "Reason");

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("*không tìm thấy*");
        }

        [Fact]
        public async Task BlockStaffShiftAsync_Unauthorized_ShouldThrowDomainException()
        {
            // Arrange
            var shiftId = 1;
            var shift = new Shift { Id = shiftId, Type = ShiftType.Cashier };
            _mockShiftRepo.Setup(s => s.GetByIdAsync(shiftId)).ReturnsAsync(shift);

            // Act
            Func<Task> act = async () => await _shiftService.BlockStaffShiftAsync(shiftId, "Reason");

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("*không có quyền*");
        }

        #endregion
    }
}
