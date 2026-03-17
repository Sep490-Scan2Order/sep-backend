using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Domain.Interfaces
{
    public interface ISubscriptionRepository : IGenericRepository<Subscription>
    {
        Task<Dictionary<int, Subscription>> GetByRestaurantIds (List<int> restaurantIds);
        Task<List<(string PlanName, int Count)>> GetSubscriptionDistributionRawAsync();

        Task<List<(int RestaurantId, string RestaurantName, string PlanName, DateTime ExpirationDate)>>
    GetExpiringSubscriptionsRawAsync(DateTime now, DateTime targetDate);
    }
}
