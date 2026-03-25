using ScanToOrder.Application.DTOs.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<SummaryMetricsResponse> GetSummaryMetricsAsync();

        Task<List<SubscriptionRevenueTrendDto>> GetSubscriptionRevenueTrendsAsync(int months = 6);

        Task<List<SubscriptionPlanDistributionDto>> GetSubscriptionPlanDistributionAsync();
        Task<List<TopPerformingRestaurantDto>> GetTopPerformingRestaurantsAsync(int top = 5);

        Task<List<ExpiringSubscriptionDto>> GetExpiringSubscriptionsAsync(int daysThreshold = 30);

        Task<List<TopTenantDto>> GetTopTenantsByRevenueAsync(int top = 10);
    }
}
