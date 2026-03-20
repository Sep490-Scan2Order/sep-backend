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
            var activeSubscriptions = await _unitOfWork.Subscriptions.FindAsync(
                s => s.RestaurantId == restaurantId && s.Status == SubscriptionStatus.Active);

            var latestSubscription = activeSubscriptions.OrderByDescending(s => s.EndDate).FirstOrDefault();

            if (latestSubscription == null)
            {
                // Fallback to default limits if no active plan
                return new PlanFeaturesConfig();
            }

            var plan = await _unitOfWork.Plans.GetByIdAsync(latestSubscription.PlanId);
            return plan?.Features ?? new PlanFeaturesConfig();
        }
    }
}
