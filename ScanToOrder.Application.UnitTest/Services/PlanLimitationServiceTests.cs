using FluentAssertions;
using Moq;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class PlanLimitationServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ISubscriptionRepository> _mockSubscriptions;
        private readonly PlanLimitationService _service;

        public PlanLimitationServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockSubscriptions = new Mock<ISubscriptionRepository>();
            _mockUnitOfWork.Setup(u => u.Subscriptions).Returns(_mockSubscriptions.Object);
            _service = new PlanLimitationService(_mockUnitOfWork.Object);
        }

        private void SetupSubscriptionQuery(Subscription? subscription)
        {
            _mockSubscriptions.Setup(s => s.GetByFieldsIncludeAsync(
                    It.IsAny<Expression<Func<Subscription, bool>>>(),
                    It.IsAny<Expression<Func<Subscription, object>>[]>()))
                .ReturnsAsync(subscription);
        }

        [Fact]
        public async Task GetRestaurantFeaturesAsync_WhenNoActiveSubscription_ReturnsDefaultConfig()
        {
            // Arrange
            int restaurantId = 1;
            SetupSubscriptionQuery(null);

            // Act
            var result = await _service.GetRestaurantFeaturesAsync(restaurantId);

            // Assert
            result.Should().NotBeNull();
            result.CanUseAIUpsell.Should().BeFalse();
        }

        [Fact]
        public async Task GetRestaurantFeaturesAsync_WhenPlanNotFound_ReturnsDefaultConfig()
        {
            // Arrange
            int restaurantId = 1;
            var subscriptionWithoutPlan = new Subscription
            {
                RestaurantId = restaurantId,
                PlanId = 99,
                Status = SubscriptionStatus.Active,
                EndDate = DateTime.UtcNow.AddDays(1),
                Plan = null!
            };

            SetupSubscriptionQuery(subscriptionWithoutPlan);

            // Act
            var result = await _service.GetRestaurantFeaturesAsync(restaurantId);

            // Assert
            result.Should().NotBeNull();
            result.CanUsePromotions.Should().BeFalse();
        }

        [Fact]
        public async Task GetRestaurantFeaturesAsync_WhenPlanFoundButFeaturesNull_ReturnsDefaultConfig()
        {
            // Arrange
            int restaurantId = 1;
            var planWithoutFeatures = new Plan { Id = 1, Features = null! };
            var subscription = new Subscription
            {
                RestaurantId = restaurantId,
                PlanId = 1,
                Status = SubscriptionStatus.Active,
                EndDate = DateTime.UtcNow.AddDays(1),
                Plan = planWithoutFeatures
            };

            SetupSubscriptionQuery(subscription);

            // Act
            var result = await _service.GetRestaurantFeaturesAsync(restaurantId);

            // Assert
            result.Should().NotBeNull();
            result.CanCustomMenuTemplate.Should().BeFalse();
        }

        [Fact]
        public async Task GetRestaurantFeaturesAsync_WhenSubscriptionAndPlanExist_ReturnsPlanFeatures()
        {
            // Arrange
            int restaurantId = 1;
            var expectedFeatures = new PlanFeaturesConfig { CanUseAIUpsell = true };
            var plan = new Plan { Id = 1, Features = expectedFeatures };
            var subscription = new Subscription
            {
                RestaurantId = restaurantId,
                PlanId = 1,
                Status = SubscriptionStatus.Active,
                EndDate = DateTime.UtcNow.AddDays(10),
                Plan = plan
            };

            SetupSubscriptionQuery(subscription);

            // Act
            var result = await _service.GetRestaurantFeaturesAsync(restaurantId);

            // Assert
            result.Should().BeEquivalentTo(expectedFeatures);
            result.CanUseAIUpsell.Should().BeTrue();
            _mockSubscriptions.Verify(s => s.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<Expression<Func<Subscription, object>>[]>()), Times.Once);
        }
    }
}