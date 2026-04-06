using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;
using ScanToOrder.Domain.Entities.SubscriptionPlan; 

namespace ScanToOrder.Application.UnitTest.Services
{
    public class PlanServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly PlanService _service;

        public PlanServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockMapper = new Mock<IMapper>();
            _service = new PlanService(_mockUnitOfWork.Object, _mockMapper.Object);
        }

        [Fact]
        public async Task GetAllPlansAsync_WhenPlansExist_ReturnsListOfPlanResponses()
        {
            // Arrange
            var mockPlans = new List<Plan> { new Plan(), new Plan() };
            var mockPlanResponses = new List<PlanResponse> { new PlanResponse(), new PlanResponse() };

            _mockUnitOfWork.Setup(u => u.Plans.GetAllAsync(
                    It.IsAny<Expression<Func<Plan, bool>>>(),
                    It.IsAny<Expression<Func<Plan, object>>[]>()))
                .ReturnsAsync(mockPlans);

            _mockMapper.Setup(m => m.Map<List<PlanResponse>>(It.IsAny<List<Plan>>()))
                .Returns(mockPlanResponses);

            // Act
            var result = await _service.GetAllPlansAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(mockPlanResponses);
        }

        [Fact]
        public async Task GetAllPlansAsync_WhenNoPlansExist_ReturnsEmptyList()
        {
            // Arrange
            var emptyPlans = new List<Plan>();
            var emptyPlanResponses = new List<PlanResponse>();

            _mockUnitOfWork.Setup(u => u.Plans.GetAllAsync(
                    It.IsAny<Expression<Func<Plan, bool>>>(),
                    It.IsAny<Expression<Func<Plan, object>>[]>()))
                .ReturnsAsync(emptyPlans);

            _mockMapper.Setup(m => m.Map<List<PlanResponse>>(It.IsAny<List<Plan>>()))
                .Returns(emptyPlanResponses);

            // Act
            var result = await _service.GetAllPlansAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
        
        #region DTO Coverage 
        [Fact]
        public void PlanResponse_Properties_GetAndSetCorrectly()
        {
            // Arrange
            var mockFeatures = new PlanFeaturesResponse();

            // Act
            var dto = new PlanResponse
            {
                Id = 1,
                Name = "Gói Pro",
                MonthlyPrice = 500000m,
                YearlyPrice = 5000000m,
                DailyRateMonth = 16000m,
                DailyRateYear = 13000m,
                Level = 2,
                Status = "Active",
                Features = mockFeatures
            };

            // Assert
            dto.Id.Should().Be(1);
            dto.Name.Should().Be("Gói Pro");
            dto.MonthlyPrice.Should().Be(500000m);
            dto.YearlyPrice.Should().Be(5000000m);
            dto.DailyRateMonth.Should().Be(16000m);
            dto.DailyRateYear.Should().Be(13000m);
            dto.Level.Should().Be(2);
            dto.Status.Should().Be("Active");
            dto.Features.Should().Be(mockFeatures);
        }
        #endregion
    }
}