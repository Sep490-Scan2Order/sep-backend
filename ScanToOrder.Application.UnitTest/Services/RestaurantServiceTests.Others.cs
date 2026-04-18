using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Models;

namespace ScanToOrder.Application.UnitTest.Services;

public partial class RestaurantServiceTests
{
    #region 10. UpdateReceivingOrdersAsync
    [Fact]
    public async Task UpdateReceivingOrdersAsync_NotFound_ReturnsNull()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync((Restaurant?)null);
        var result = await _service.UpdateReceivingOrdersAsync(1, true);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateReceivingOrdersAsync_Valid_ReturnsMessageAndNotifies()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "test", IsReceivingOrders = false };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);
        var result = await _service.UpdateReceivingOrdersAsync(1, true);
        result.Should().Be("Cập nhật trạng thái nhận đơn của nhà hàng thành công.");
        _mockRealtimeService.Verify(r => r.NotifyReceivingOrdersChanged("1", true), Times.Once);
    }
    #endregion

    #region 11. UpdateOpeningStatusAsync
    [Fact]
    public async Task UpdateOpeningStatusAsync_NotFound_ReturnsNull()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync((Restaurant?)null);
        var result = await _service.UpdateOpeningStatusAsync(1, true);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateOpeningStatusAsync_Closing_AlsoSetsReceivingOrdersFalse()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "test", IsOpened = true, IsReceivingOrders = true };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);
        var result = await _service.UpdateOpeningStatusAsync(1, false);
        restaurant.IsOpened.Should().BeFalse();
        restaurant.IsReceivingOrders.Should().BeFalse();
    }
    #endregion

    #region 12. UpdateActiveStatusAsync
    [Fact]
    public async Task UpdateActiveStatusAsync_NotFound_ReturnsNull()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdIncludeSubscriptionAsync(1)).ReturnsAsync((Restaurant?)null);
        var result = await _service.UpdateActiveStatusAsync(1, true);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateActiveStatusAsync_ExpiredSubscription_ReturnsErrorMessage()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "test", Subscription = new Subscription { Status = SubscriptionStatus.Expired } };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdIncludeSubscriptionAsync(1)).ReturnsAsync(restaurant);
        var result = await _service.UpdateActiveStatusAsync(1, true);
        result.Should().Be("Không thể kích hoạt nhà hàng khi subscription đã hết hạn");
    }

    [Fact]
    public async Task UpdateActiveStatusAsync_Deactivating_ClosesAndStopsReceiving()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "test", IsActive = true, IsOpened = true, IsReceivingOrders = true, Subscription = new Subscription { Status = SubscriptionStatus.Active } };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdIncludeSubscriptionAsync(1)).ReturnsAsync(restaurant);
        var result = await _service.UpdateActiveStatusAsync(1, false);
        restaurant.IsActive.Should().BeFalse();
        restaurant.IsOpened.Should().BeFalse();
        restaurant.IsReceivingOrders.Should().BeFalse();
    }
    #endregion

    #region 13. AssignPresentCashier
    [Fact]
    public async Task AssignPresentCashier_RestaurantNotFound_Throws()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync((Restaurant?)null);
        Func<Task> act = async () => await _service.AssignPresentCashier(1, Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task AssignPresentCashier_StaffNotFound_Throws()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Id = 1, Slug = "test" });
        _mockUnitOfWork.Setup(u => u.Staffs.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Staff?)null);
        Func<Task> act = async () => await _service.AssignPresentCashier(1, Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task AssignPresentCashier_StaffNotInRestaurant_Throws()
    {
        var staff = new Staff { Id = Guid.NewGuid(), RestaurantId = 2 };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Id = 1, Slug = "test" });
        _mockUnitOfWork.Setup(u => u.Staffs.GetByIdAsync(staff.Id)).ReturnsAsync(staff);
        Func<Task> act = async () => await _service.AssignPresentCashier(1, staff.Id);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task AssignPresentCashier_Valid_Success()
    {
        var staff = new Staff { Id = Guid.NewGuid(), Name = "John", RestaurantId = 1 };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Id = 1, Slug = "test" });
        _mockUnitOfWork.Setup(u => u.Staffs.GetByIdAsync(staff.Id)).ReturnsAsync(staff);
        var result = await _service.AssignPresentCashier(1, staff.Id);
        result.CashierName.Should().Be("John");
    }
    #endregion

    #region 14. ConfigMinCashAmountAsync
    [Fact]
    public async Task ConfigMinCashAmountAsync_NotFound_Throws()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync((Restaurant?)null);
        Func<Task> act = async () => await _service.ConfigMinCashAmountAsync(1, 50000m);
        await act.Should().ThrowAsync<DomainException>();
    }
    [Fact]
    public async Task ConfigMinCashAmountAsync_Valid_SetsAmount()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "test" };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);
        var result = await _service.ConfigMinCashAmountAsync(1, 50000m);
        restaurant.MinCashAmount.Should().Be(50000m);
    }
    #endregion

    #region 15. GetRevenueSummaryAsync & GetSuggestionRestaurantAsync
    [Fact]
    public async Task GetRevenueSummaryAsync_NoOrders_ReturnsZeroAverageOrderValue()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Id = 1, Slug = "test" });
        var stats = new OrderRevenueMetrics { TotalOrders = 0, GrossRevenue = 0m, NetRevenue = 0m };
        _mockUnitOfWork.Setup(u => u.Orders.GetRevenueSummaryAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(stats);
        _mockUnitOfWork.Setup(u => u.Orders.GetTopSellingDishesAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 10)).ReturnsAsync(new List<(int, string, int, decimal)>());
        _mockUnitOfWork.Setup(u => u.ShiftReports.GetPaymentMetricsAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync((0m, 0m, 0m));

        var result = await _service.GetRevenueSummaryAsync(1, DateTime.Today, DateTime.Today);
        result.Summary.AverageOrderValue.Should().Be(0);
    }
    [Fact]
    public async Task GetSuggestionRestaurantAsync_ReturnsMappedDtos()
    {
        var restaurants = new List<Restaurant> { new Restaurant { Id = 1, Slug = "test" } };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetSuggesstRestaurantAsync()).ReturnsAsync(restaurants);
        _mockMapper.Setup(m => m.Map<IEnumerable<RestaurantDto>>(restaurants)).Returns(new List<RestaurantDto> { new RestaurantDto { Id = 1 } });
        var result = await _service.GetSuggestionRestaurantAsync();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRevenueSummaryAsync_NotFound_Throws()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync((Restaurant?)null);
        Func<Task> act = async () => await _service.GetRevenueSummaryAsync(1, DateTime.Today, DateTime.Today);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetRevenueSummaryAsync_Valid_MapsSummaryCorrectly()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Id = 1, Slug = "test" });
        var stats = new OrderRevenueMetrics { TotalOrders = 10, GrossRevenue = 1000m, NetRevenue = 900m, TotalDiscount = 100m, RegularCount = 9, RegularRevenue = 950m, RefundCount = 1, RefundRevenue = 50m, RefundObjectiveCount = 0, RefundObjectiveRevenue = 0m, RefundStaffErrorCount = 1, RefundStaffErrorRevenue = 50m, RefundSystemErrorCount = 0, RefundSystemErrorRevenue = 0m, TotalCash = 300m, TotalTransfer = 600m };
        _mockUnitOfWork.Setup(u => u.Orders.GetRevenueSummaryAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(stats);
        var topDishes = new List<(int DishId, string DishName, int QuantitySold, decimal Revenue)> { (1, "Dish 1", 5, 200m) };
        _mockUnitOfWork.Setup(u => u.Orders.GetTopSellingDishesAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 10)).ReturnsAsync(topDishes);
        var shiftStats = ( TotalCash: 0m, TotalTransfer: 0m, TotalRefund: 50m );
        _mockUnitOfWork.Setup(u => u.ShiftReports.GetPaymentMetricsAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(shiftStats);

        var result = await _service.GetRevenueSummaryAsync(1, DateTime.Today, DateTime.Today);
        result.Summary.TotalOrders.Should().Be(10);
        result.Summary.NetRevenue.Should().Be(900m);
        result.PaymentMethods.Cash.Should().Be(300m);
    }
    #endregion
}


