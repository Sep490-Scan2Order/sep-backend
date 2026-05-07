using System;

namespace ScanToOrder.Application.DTOs.Orders;

public class PaymentQrRequest
{
    public string CartId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    
    public bool? IsPreOrder { get; set; }

    public DateTime? RequestedPickupAt { get; set; }
    
    public int? AppliedPromotionId { get; set; }
}

