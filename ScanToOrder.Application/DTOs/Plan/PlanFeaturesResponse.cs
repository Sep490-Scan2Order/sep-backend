namespace ScanToOrder.Application.DTOs.Plan;

public class PlanFeaturesResponse
{
    public bool CanUseAIUpsell { get; set; }
    public bool CanRecommendationOnTop { get; set; } 
    public bool CanUsePromotions { get; set; }
    public bool CanCustomMenuTemplate { get; set; }
}