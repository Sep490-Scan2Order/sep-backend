using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan;

public class OrderPayloadItemPlan
{
    public int RestaurantId { get; set; }
    public SubscriptionLogStatus ActionType { get; set; }
    public int? OldPlanId { get; set; }
    public int NewPlanId { get; set; }
    public BillingCycle Cycle { get; set; }
    public int Quantity { get; set; }
    public decimal AmountAllocated { get; set; }
    public decimal BalanceConverted { get; set; }
}