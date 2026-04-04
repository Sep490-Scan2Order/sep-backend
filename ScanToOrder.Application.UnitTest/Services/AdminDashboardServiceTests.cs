using FluentAssertions;
using Moq;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services;

public class AdminDashboardServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly AdminDashboardService _adminDashboardService;

    public AdminDashboardServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _adminDashboardService = new AdminDashboardService(_mockUnitOfWork.Object);
    }

    #region 1. GetSummaryMetricsAsync
    [Fact]
    public async Task GetSummaryMetricsAsync_ReturnsCorrectMetrics()
    {
        // Arrange
        _mockUnitOfWork.Setup(u => u.Tenants.CountAsync(It.IsAny<Expression<Func<Tenant, bool>>>())).ReturnsAsync(10);
        _mockUnitOfWork.Setup(u => u.Restaurants.CountAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(25);
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.CountAsync(It.IsAny<Expression<Func<AuthenticationUser, bool>>>())).ReturnsAsync(8);

        _mockUnitOfWork.Setup(u => u.PaymentTransactions.FindAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>()))
            .ReturnsAsync(new List<PaymentTransaction>());

        _mockUnitOfWork.Setup(u => u.PaymentTransactions.SumAsync(
                It.IsAny<Expression<Func<PaymentTransaction, bool>>>(),
                It.IsAny<Expression<Func<PaymentTransaction, decimal>>>()))
            .ReturnsAsync(5000000m);

        // Act
        var result = await _adminDashboardService.GetSummaryMetricsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalTenants.Should().Be(10);
        result.TotalRestaurants.Should().Be(25);
        result.ActiveAccounts.Should().Be(8);
        result.PlatformRevenue.Should().Be(5000000m);
    }
    #endregion

    #region 2. GetSubscriptionRevenueTrendsAsync
    [Fact]
    public async Task GetSubscriptionRevenueTrendsAsync_ReturnsMappedData()
    {
        // Arrange
        var rawData = new List<(int Year, int Month, decimal Revenue)>
        {
            (2026, 4, 1000m),
            (2026, 5, 1500m)
        };
        _mockUnitOfWork.Setup(u => u.PaymentTransactions.GetRevenueTrendRawAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(rawData);

        // Act
        var result = await _adminDashboardService.GetSubscriptionRevenueTrendsAsync(6);

        // Assert
        result.Should().HaveCount(2);
        result[0].Month.Should().Be("4/2026");
        result[0].Revenue.Should().Be(1000m);
    }
    #endregion

    #region 3. GetSubscriptionPlanDistributionAsync
    [Fact]
    public async Task GetSubscriptionPlanDistributionAsync_WhenTotalIsZero_ReturnsEmptyList()
    {
        // Arrange
        var rawData = new List<(string PlanName, int Count)>();
        _mockUnitOfWork.Setup(u => u.Subscriptions.GetSubscriptionDistributionRawAsync())
            .ReturnsAsync(rawData);

        // Act
        var result = await _adminDashboardService.GetSubscriptionPlanDistributionAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSubscriptionPlanDistributionAsync_WhenTotalGreaterThanZero_ReturnsCalculatedPercentages()
    {
        // Arrange
        var rawData = new List<(string PlanName, int Count)>
        {
            ("Basic", 1),
            ("Pro", 3)
        };
        _mockUnitOfWork.Setup(u => u.Subscriptions.GetSubscriptionDistributionRawAsync())
            .ReturnsAsync(rawData);

        // Act
        var result = await _adminDashboardService.GetSubscriptionPlanDistributionAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].PlanName.Should().Be("Basic");
        result[0].Percentage.Should().Be(25);
        result[1].Percentage.Should().Be(75);
    }
    #endregion

    #region 4. GetTopPerformingRestaurantsAsync 
    [Fact]
    public async Task GetTopPerformingRestaurantsAsync_WhenNoData_ReturnsEmptyList()
    {
        // Arrange
        var rawData = new List<(int RestaurantId, string RestaurantName, string Image, int TotalOrders, decimal TotalRevenue, string PlanName, SubscriptionStatus? Status)>();
        _mockUnitOfWork.Setup(u => u.Orders.GetTopRestaurantsFullDataAsync(It.IsAny<int>()))
            .ReturnsAsync(rawData);

        // Act
        var result = await _adminDashboardService.GetTopPerformingRestaurantsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopPerformingRestaurantsAsync_ReturnsData_CoversGetPlanNameBranches()
    {
        // Arrange
        var rawData = new List<(int RestaurantId, string RestaurantName, string Image, int TotalOrders, decimal TotalRevenue, string PlanName, SubscriptionStatus? Status)>
        {
            (1, "Res 1", "img1", 10, 100m, "Pro", SubscriptionStatus.Active),
            (2, "Res 2", "img2", 5, 50m, "Basic", SubscriptionStatus.Cancel),
            (3, "Res 3", "img3", 2, 20m, null, SubscriptionStatus.Active)
        };

        _mockUnitOfWork.Setup(u => u.Orders.GetTopRestaurantsFullDataAsync(It.IsAny<int>()))
            .ReturnsAsync(rawData);

        // Act
        var result = await _adminDashboardService.GetTopPerformingRestaurantsAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].CurrentPlan.Should().Be("Pro");
        result[1].CurrentPlan.Should().Be("No Active Plan");
        result[2].CurrentPlan.Should().Be("Unknown");
    }
    #endregion

    #region 5. GetExpiringSubscriptionsAsync 
    [Fact]
    public async Task GetExpiringSubscriptionsAsync_ReturnsMappedDataWithDaysRemaining()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var rawData = new List<(int RestaurantId, string RestaurantName, string PlanName, DateTime ExpirationDate)>
        {
            (1, "Res 1", "Pro", now.AddDays(5).AddHours(2))
        };
        _mockUnitOfWork.Setup(u => u.Subscriptions.GetExpiringSubscriptionsRawAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(rawData);

        // Act
        var result = await _adminDashboardService.GetExpiringSubscriptionsAsync(30);

        // Assert
        result.Should().HaveCount(1);
        result[0].DaysRemaining.Should().Be(5);
    }
    #endregion

    #region 6. GetTopTenantsByRevenueAsync
    [Fact]
    public async Task GetTopTenantsByRevenueAsync_ReturnsData()
    {
        // Arrange
        var rawData = new List<(Guid TenantId, string TenantName, int TotalRestaurants, int TotalOrders, decimal TotalRevenue)>
        {
            (Guid.NewGuid(), "Tenant 1", 2, 100, 5000m)
        };
        _mockUnitOfWork.Setup(u => u.Orders.GetTopTenantsByRevenueAsync(It.IsAny<int>()))
            .ReturnsAsync(rawData);

        // Act
        var result = await _adminDashboardService.GetTopTenantsByRevenueAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].TenantName.Should().Be("Tenant 1");
    }
    #endregion

    #region 7. GetTenantDetailAsync
    [Fact]
    public async Task GetTenantDetailAsync_WhenTenantNotFound_ThrowsDomainException()
    {
        // Arrange
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant)null);

        // Act
        var action = async () => await _adminDashboardService.GetTenantDetailAsync(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow);

        // Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage("Tenant * not found.");
    }

    [Fact]
    public async Task GetTenantDetailAsync_WhenTenantExists_ReturnsDetail_CoversAllBranches()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = null, IsSuspended = false };

        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

        var restaurants = new List<Restaurant>
    {
        new Restaurant { Id = 1, RestaurantName = "Res 1", Slug = "res-1", IsActive = null, Subscription = new Subscription { Status = SubscriptionStatus.Active, Plan = new Plan { Name = "Basic" } } },

        new Restaurant { Id = 2, RestaurantName = "Res 2", Slug = "res-2", IsActive = true, Subscription = new Subscription { Status = SubscriptionStatus.Cancel } },

        new Restaurant { Id = 3, RestaurantName = "Res 3", Slug = "res-3", IsActive = false, Subscription = null }
    };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId))
            .ReturnsAsync(restaurants);

        var revenueRawData = new List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>
    {
        (1, 10, 1000m, 900m, 100m)
    };
        _mockUnitOfWork.Setup(u => u.Orders.GetRevenueByTenantAsync(tenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(revenueRawData);

        // Act
        var result = await _adminDashboardService.GetTenantDetailAsync(tenantId, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        // Assert
        result.Should().NotBeNull();
        result.TenantName.Should().Be(string.Empty);

        result.Restaurants.Should().HaveCount(3); 

        var res1 = result.Restaurants.First(r => r.RestaurantId == 1);
        res1.IsActive.Should().BeFalse();
        res1.CurrentPlan.Should().Be("Basic");
        res1.GrossRevenue.Should().Be(1000m);

        var res2 = result.Restaurants.First(r => r.RestaurantId == 2);
        res2.IsActive.Should().BeTrue();
        res2.CurrentPlan.Should().Be("No Active Plan");
        res2.GrossRevenue.Should().Be(0m);

        var res3 = result.Restaurants.First(r => r.RestaurantId == 3);
        res3.CurrentPlan.Should().Be("No Active Plan");
    }
    #endregion
}