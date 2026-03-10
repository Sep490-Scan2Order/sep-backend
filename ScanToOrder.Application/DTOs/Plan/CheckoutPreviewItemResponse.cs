using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.Plan;

public class CheckoutPreviewItemResponse
{
    public int RestaurantId { get; set; }
    public string RestaurantName { get; set; }
    
    public SubscriptionLogStatus ActionType { get; set; } 
    
    public string TargetPlanName { get; set; }
    public BillingCycle Cycle { get; set; }
    public int Quantity { get; set; }

    public decimal BasePrice { get; set; }         
    public decimal BalanceConverted { get; set; }  
    public decimal AmountToPay { get; set; }
    
    public string Message { get; set; }
}