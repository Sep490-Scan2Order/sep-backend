using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Dashboard;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private readonly IAdminDashboardService _dashboardService;
        private readonly ICronJobService _cronJobService;

        public AdminController(IAdminDashboardService dashboardService, ICronJobService cronJobService)
        {
            _dashboardService = dashboardService;
            _cronJobService = cronJobService;
        }

        [HttpGet("summary-metrics")]
        public async Task<ActionResult<ApiResponse<SummaryMetricsResponse>>> GetSummaryMetrics()
        {
            var result = await _dashboardService.GetSummaryMetricsAsync();
            return Success(result);
        }

        [HttpGet("subscription-revenue-trends")]
        public async Task<ActionResult<ApiResponse<List<SubscriptionRevenueTrendDto>>>> GetSubscriptionRevenueTrends([FromQuery] int months = 6)
        {
            var result = await _dashboardService.GetSubscriptionRevenueTrendsAsync(months);
            return Success(result);
        }

        [HttpGet("commission-revenue-trends")]
        public async Task<ActionResult<ApiResponse<List<CommissionFeeRevenueTrendDto>>>> GetCommissionFeeRevenueTrends([FromQuery] int months = 6)
        {
            var result = await _dashboardService.GetCommissionFeeRevenueTrendsAsync(months);
            return Success(result);
        }

        [HttpGet("subscription-revenue-by-plan")]
        public async Task<ActionResult<ApiResponse<List<SubscriptionRevenueByPlanDto>>>> GetSubscriptionRevenueByPlan([FromQuery] int months = 6)
        {
            var result = await _dashboardService.GetSubscriptionRevenueByPlanAsync(months);
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

        [HttpGet("top-tenants")]
        public async Task<ActionResult<ApiResponse<List<TopTenantDto>>>> GetTopTenantsByRevenue([FromQuery] int top = 10)
        {
            var result = await _dashboardService.GetTopTenantsByRevenueAsync(top);
            return Success(result);
        }

        [HttpGet("tenants/{tenantId}/detail")]
        public async Task<ActionResult<ApiResponse<TenantDetailDto>>> GetTenantDetail(
            Guid tenantId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var end   = endDate   ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
            var result = await _dashboardService.GetTenantDetailAsync(tenantId, start, end);
            return Success(result);
        }

        [HttpPost("test-cronjobs")]
        public async Task<ActionResult<ApiResponse<string>>> TestCronJobs()
        {
            await _cronJobService.CalculateWeeklyCommissionFeeAsync(CancellationToken.None);
            await _cronJobService.MonitorAndSuspendOverdueDebtsAsync(CancellationToken.None);
            return Success("Cron jobs executed", "Đã chạy test cronjob thành công.");
        }
    }
}
