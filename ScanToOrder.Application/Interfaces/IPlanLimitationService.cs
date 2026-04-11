using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Application.Interfaces
{
    public interface IPlanLimitationService
    {
        /// <summary>Gets the PlanFeaturesConfig of a restaurant with an active subscription.</summary>
        Task<PlanFeaturesConfig> GetRestaurantFeaturesAsync(int restaurantId);
    }
}
