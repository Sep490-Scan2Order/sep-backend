namespace ScanToOrder.Domain.Enums;

public class OrderPayloadItemPlan
{
    public int RestaurantId { get; set; }
    public SubscriptionLogStatus ActionType { get; set; }
    public int? OldPlanId { get; set; }
    public int NewPlanId { get; set; }
    public int DurationInMonths { get; set; }
    public decimal AmountAllocated { get; set; }
    public decimal BalanceConverted { get; set; }
}