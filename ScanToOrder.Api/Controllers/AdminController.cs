using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Dashboard;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class AdminController : BaseController
    {
        private readonly IAdminDashboardService _dashboardService;

        public AdminController(IAdminDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary-metrics")]
        public async Task<ActionResult<ApiResponse<SummaryMetricsResponse>>> GetSummaryMetrics()
        {
            var result = await _dashboardService.GetSummaryMetricsAsync();
            return Success(result);
        }


        [HttpGet("revenue-trends")]
        public async Task<ActionResult<ApiResponse<List<SubscriptionRevenueTrendDto>>>> GetSubscriptionRevenueTrends([FromQuery] int months = 6)
        {
            var result = await _dashboardService.GetSubscriptionRevenueTrendsAsync(months);
            return Success(result);
        }

        [HttpGet("plan-distribution")]
        public async Task<ActionResult<ApiResponse<List<SubscriptionPlanDistributionDto>>>> GetSubscriptionPlanDistribution()
        {
            var result = await _dashboardService.GetSubscriptionPlanDistributionAsync();
            return Success(result);
        }

        [HttpGet("top-performing-restaurants")]
        public async Task<ActionResult<ApiResponse<List<TopPerformingRestaurantDto>>>> GetTopPerformingRestaurants([FromQuery] int top = 5)
        {
            var result = await _dashboardService.GetTopPerformingRestaurantsAsync(top);
            return Success(result);
        }

        [HttpGet("expiring-subscriptions")]
        public async Task<ActionResult<ApiResponse<List<ExpiringSubscriptionDto>>>> GetExpiringSubscriptions([FromQuery] int daysThreshold = 30)
        {
            var result = await _dashboardService.GetExpiringSubscriptionsAsync(daysThreshold);
            return Success(result);
        }
    }
}
