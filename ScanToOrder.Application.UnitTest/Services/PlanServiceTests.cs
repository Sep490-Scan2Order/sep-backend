using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

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

        #region 1. GetAllPlansAsync

        [Fact]
        public async Task GetAllPlansAsync_WhenPlansExist_ReturnsListOfPlanResponses()
        {
            // Arrange
            var mockPlans = new List<Plan> { new Plan(), new Plan() };
            var mockPlanResponses = new List<PlanResponse> { new PlanResponse(), new PlanResponse() };

            // Khai báo rõ ràng It.IsAny để tránh lỗi CS0854
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

        #endregion

        #region 2. GetPlanByIdAsync

        [Fact]
        public async Task GetPlanByIdAsync_WhenPlanDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            int planId = 1;
            _mockUnitOfWork.Setup(u => u.Plans.GetByIdAsync(planId))
                .ReturnsAsync((Plan)null);

            // Act
            Func<Task> action = async () => await _service.GetPlanByIdAsync(planId);

            // Assert
            await action.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"Không tìm thấy gói dịch vụ với Id = {planId}.");
        }

        [Fact]
        public async Task GetPlanByIdAsync_WhenPlanExists_ReturnsPlanResponse()
        {
            // Arrange
            int planId = 1;
            var plan = new Plan { Id = planId, Name = "Basic" };
            var planResponse = new PlanResponse { Id = planId, Name = "Basic" };

            _mockUnitOfWork.Setup(u => u.Plans.GetByIdAsync(planId))
                .ReturnsAsync(plan);

            _mockMapper.Setup(m => m.Map<PlanResponse>(plan))
                .Returns(planResponse);

            // Act
            var result = await _service.GetPlanByIdAsync(planId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(planId);
            result.Name.Should().Be("Basic");
        }

        #endregion

        #region 3. CreatePlanAsync

        [Fact]
        public async Task CreatePlanAsync_WhenNameExists_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new CreatePlanRequest { Name = "ExistingPlan" };

            _mockUnitOfWork.Setup(u => u.Plans.ExistsAsync(It.IsAny<Expression<Func<Plan, bool>>>()))
                .ReturnsAsync(true);

            // Act
            Func<Task> action = async () => await _service.CreatePlanAsync(request);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"Gói dịch vụ với tên '{request.Name}' đã tồn tại.");
        }

        [Fact]
        public async Task CreatePlanAsync_WhenNameIsUnique_CreatesAndReturnsPlanResponse()
        {
            // Arrange
            // Đã xóa gán Features = ... để fix lỗi CS0246
            var request = new CreatePlanRequest { Name = "NewPlan" };
            var plan = new Plan { Name = "NewPlan" };
            var featuresConfig = new PlanFeaturesConfig();
            var planResponse = new PlanResponse { Name = "NewPlan" };

            _mockUnitOfWork.Setup(u => u.Plans.ExistsAsync(It.IsAny<Expression<Func<Plan, bool>>>()))
                .ReturnsAsync(false);

            _mockMapper.Setup(m => m.Map<Plan>(request)).Returns(plan);
            // Moq map thuộc tính Features (dù null hay không vẫn Map an toàn)
            _mockMapper.Setup(m => m.Map<PlanFeaturesConfig>(request.Features)).Returns(featuresConfig);
            _mockMapper.Setup(m => m.Map<PlanResponse>(plan)).Returns(planResponse);

            // Act
            var result = await _service.CreatePlanAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("NewPlan");
            plan.Features.Should().Be(featuresConfig);

            _mockUnitOfWork.Verify(u => u.Plans.AddAsync(plan), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        #endregion

        #region 4. UpdatePlanAsync

        [Fact]
        public async Task UpdatePlanAsync_WhenPlanDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            int planId = 1;
            var request = new UpdatePlanRequest { Name = "UpdatedPlan" };

            _mockUnitOfWork.Setup(u => u.Plans.GetByIdAsync(planId))
                .ReturnsAsync((Plan)null);

            // Act
            Func<Task> action = async () => await _service.UpdatePlanAsync(planId, request);

            // Assert
            await action.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"Không tìm thấy gói dịch vụ với Id = {planId}.");
        }

        [Fact]
        public async Task UpdatePlanAsync_WhenNameConflictsWithOtherPlan_ThrowsInvalidOperationException()
        {
            // Arrange
            int planId = 1;
            var plan = new Plan { Id = planId, Name = "OldPlan" };
            var request = new UpdatePlanRequest { Name = "DuplicateName" };

            _mockUnitOfWork.Setup(u => u.Plans.GetByIdAsync(planId))
                .ReturnsAsync(plan);

            _mockUnitOfWork.Setup(u => u.Plans.ExistsAsync(It.IsAny<Expression<Func<Plan, bool>>>()))
                .ReturnsAsync(true);

            // Act
            Func<Task> action = async () => await _service.UpdatePlanAsync(planId, request);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"Gói dịch vụ với tên '{request.Name}' đã tồn tại.");
        }

        [Fact]
        public async Task UpdatePlanAsync_WhenValid_UpdatesAndReturnsPlanResponse()
        {
            // Arrange
            int planId = 1;
            var plan = new Plan { Id = planId, Name = "OldPlan" };
            // Đã xóa gán Features = ... để fix lỗi CS0246
            var request = new UpdatePlanRequest { Name = "UpdatedPlan" };
            var featuresConfig = new PlanFeaturesConfig();
            var planResponse = new PlanResponse { Id = planId, Name = "UpdatedPlan" };

            _mockUnitOfWork.Setup(u => u.Plans.GetByIdAsync(planId))
                .ReturnsAsync(plan);

            _mockUnitOfWork.Setup(u => u.Plans.ExistsAsync(It.IsAny<Expression<Func<Plan, bool>>>()))
                .ReturnsAsync(false);

            _mockMapper.Setup(m => m.Map(request, plan)).Returns(plan);
            _mockMapper.Setup(m => m.Map<PlanFeaturesConfig>(request.Features)).Returns(featuresConfig);
            _mockMapper.Setup(m => m.Map<PlanResponse>(plan)).Returns(planResponse);

            // Act
            var result = await _service.UpdatePlanAsync(planId, request);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("UpdatedPlan");
            plan.Features.Should().Be(featuresConfig);

            _mockUnitOfWork.Verify(u => u.Plans.Update(plan), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        #endregion

        #region 5. DTO Coverage 

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