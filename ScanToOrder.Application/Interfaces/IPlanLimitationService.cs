using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Application.Interfaces
{
    public interface IPlanLimitationService
    {
        Task<PlanFeaturesConfig> GetRestaurantFeaturesAsync(int restaurantId);
    }
}
