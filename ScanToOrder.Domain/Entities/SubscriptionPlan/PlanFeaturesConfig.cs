namespace ScanToOrder.Domain.Entities.SubscriptionPlan;

public class PlanFeaturesConfig
{
    public bool CanUseAIUpsell { get; set; } = false;
    public bool CanRecommendationOnTop { get; set; } = false;
    public bool CanUsePromotions { get; set; } = false;
    public bool CanCustomMenuTemplate { get; set; } = false;
}