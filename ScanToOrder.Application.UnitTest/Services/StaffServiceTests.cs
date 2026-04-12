using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class StaffServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IStaffRepository> _mockStaffRepo;
        private readonly Mock<IRestaurantRepository> _mockRestaurantRepo;
        private readonly Mock<IAuthenticationUserRepository> _mockAuthUserRepo;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IPlanLimitationService> _mockPlanLimitationService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly StaffService _staffService;

        public StaffServiceTests()
        {
            _mockStaffRepo = new Mock<IStaffRepository>();
            _mockRestaurantRepo = new Mock<IRestaurantRepository>();
            _mockAuthUserRepo = new Mock<IAuthenticationUserRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockPlanLimitationService = new Mock<IPlanLimitationService>();
            _mockEmailService = new Mock<IEmailService>();

            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUnitOfWork.Setup(u => u.Staffs).Returns(_mockStaffRepo.Object);
            _mockUnitOfWork.Setup(u => u.Restaurants).Returns(_mockRestaurantRepo.Object);
            _mockUnitOfWork.Setup(u => u.AuthenticationUsers).Returns(_mockAuthUserRepo.Object);

            _staffService = new StaffService(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockPlanLimitationService.Object,
                _mockEmailService.Object);
        }

        #region 1. CreateStaff Tests

        // Tests successful staff creation including dependencies like PlanLimitation and EmailService.
        [Fact]
        public async Task CreateStaff_Success_ShouldCreateStaffAndSendEmail()
        {
            // Arrange
            var request = new CreateStaffRequest { RestaurantId = 1, Phone = "0123456789", Email = "staff@test.com", Name = "Staff Name" };
            _mockAuthUserRepo.Setup(r => r.GetByPhoneAsync(request.Phone)).ReturnsAsync((AuthenticationUser)null);
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(request.RestaurantId)).ReturnsAsync(new Restaurant { Id = 1, Slug = "test" });
            _mockPlanLimitationService.Setup(s => s.GetRestaurantFeaturesAsync(request.RestaurantId)).ReturnsAsync(new PlanFeaturesConfig());
            _mockStaffRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Staff, bool>>>())).ReturnsAsync(new List<Staff>());
            
            _mockMapper.Setup(m => m.Map<AuthenticationUser>(request)).Returns(new AuthenticationUser { Id = Guid.NewGuid() });
            _mockMapper.Setup(m => m.Map<Staff>(request)).Returns(new Staff { Id = Guid.NewGuid() });
            _mockMapper.Setup(m => m.Map<StaffDto>(It.IsAny<Staff>())).Returns(new StaffDto { Name = "Staff Name" });

            // Act
            var result = await _staffService.CreateStaff(request);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Staff Name");
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
            _mockEmailService.Verify(e => e.SendEmailWithTemplateIdDomainAsync(
                request.Email, 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<object>()), Times.Once);
        }

        // Verifies that CreateStaff throws a DomainException if a user with the same phone already exists.
        [Fact]
        public async Task CreateStaff_AlreadyExists_ShouldThrowDomainException()
        {
            // Arrange
            var request = new CreateStaffRequest { Phone = "0123456789", RestaurantId = 1, Email = "staff@test.com", Name = "Staff" };
            _mockAuthUserRepo.Setup(r => r.GetByPhoneAsync(request.Phone)).ReturnsAsync(new AuthenticationUser());

            // Act
            Func<Task> act = async () => await _staffService.CreateStaff(request);

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }

        // Verifies that CreateStaff throws a DomainException if the specified restaurant is not found.
        [Fact]
        public async Task CreateStaff_RestaurantNotFound_ShouldThrowDomainException()
        {
            // Arrange
            var request = new CreateStaffRequest { Phone = "0123456789", RestaurantId = 1, Email = "staff@test.com", Name = "Staff" };
            _mockAuthUserRepo.Setup(r => r.GetByPhoneAsync(request.Phone)).ReturnsAsync((AuthenticationUser)null);
            _mockRestaurantRepo.Setup(r => r.GetByIdAsync(request.RestaurantId)).ReturnsAsync((Restaurant)null);

            // Act
            Func<Task> act = async () => await _staffService.CreateStaff(request);

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }

        #endregion

        #region 2. GetAllStaff Tests

        // Tests paginated retrieval of staff and verifies the hardcoded business rule that sets IsActive to true.
        [Fact]
        public async Task GetAllStaff_Success_ShouldReturnPagedResultWithIsActiveTrue()
        {
            // Arrange
            var restaurantId = 1;
            var staffList = new List<Staff> { new Staff { Id = Guid.NewGuid() } };
            _mockStaffRepo.Setup(r => r.GetStaffByRestaurantAsync(restaurantId, 1, 10)).ReturnsAsync((staffList, 1));
            _mockMapper.Setup(m => m.Map<List<StaffDto>>(staffList)).Returns(new List<StaffDto> { new StaffDto { IsActive = false } });

            // Act
            var result = await _staffService.GetAllStaff(restaurantId, 1, 10);

            // Assert
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(1);
            result.Items.First().IsActive.Should().BeTrue();
        }

        #endregion

        #region 3. GetAvailableCashiers Tests

        // Verifies that available cashiers are correctly retrieved and mapped.
        [Fact]
        public async Task GetAvailableCashiers_Success_ShouldReturnList()
        {
            // Arrange
            var cashiers = new List<Staff> { new Staff { Id = Guid.NewGuid() } };
            _mockStaffRepo.Setup(r => r.GetAvailableCashiersAsync()).ReturnsAsync(cashiers);
            _mockMapper.Setup(m => m.Map<List<StaffDto>>(cashiers)).Returns(new List<StaffDto> { new StaffDto() });

            // Act
            var result = await _staffService.GetAvailableCashiers();

            // Assert
            result.Should().HaveCount(1);
        }

        #endregion

        #region 4. GetStaffByRestaurant Tests

        // Verifies that all staff for a restaurant can be retrieved via a generic find query.
        [Fact]
        public async Task GetStaffByRestaurant_Success_ShouldReturnList()
        {
            // Arrange
            var restaurantId = 1;
            var staffList = new List<Staff> { new Staff { Id = Guid.NewGuid(), RestaurantId = restaurantId } };
            _mockStaffRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Staff, bool>>>())).ReturnsAsync(staffList);
            _mockMapper.Setup(m => m.Map<List<StaffDto>>(staffList)).Returns(new List<StaffDto> { new StaffDto() });

            // Act
            var result = await _staffService.GetStaffByRestaurant(restaurantId);

            // Assert
            result.Should().HaveCount(1);
        }

        #endregion
    }
}
