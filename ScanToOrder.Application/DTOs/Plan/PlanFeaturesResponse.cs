namespace ScanToOrder.Application.DTOs.Plan;

public class PlanFeaturesResponse
{
    public int MaxStaff { get; set; }
    public bool CanUseCombo { get; set; }
    public bool CanUsePromotions { get; set; }
    public bool CanCustomMenuTemplate { get; set; }
}