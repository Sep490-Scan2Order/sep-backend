namespace ScanToOrder.Domain.Entities.SubscriptionPlan;

public class PlanFeaturesConfig
{
    public int MaxStaff { get; set; } = 2;
    public bool CanUseCombo { get; set; } = false;
    public bool CanUsePromotions { get; set; } = false;
    public bool CanCustomMenuTemplate { get; set; } = false;
}