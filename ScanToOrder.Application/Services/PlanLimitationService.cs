using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class PlanLimitationService : IPlanLimitationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PlanLimitationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PlanFeaturesConfig> GetRestaurantFeaturesAsync(int restaurantId)
        {
            // Single query: load Subscription along with Plan.Features (to prevent N+1 queries)
            var subscription = await _unitOfWork.Subscriptions.GetByFieldsIncludeAsync(
                s => s.RestaurantId == restaurantId && s.Status == SubscriptionStatus.Active,
                s => s.Plan
            );

            return subscription?.Plan?.Features ?? new PlanFeaturesConfig();
        }
    }
}
