using System;

namespace ScanToOrder.Application.DTOs.Orders;

public class CashCheckoutResponse
{
    public Guid OrderId { get; set; }
    public int OrderCode { get; set; }
    public decimal TotalAmount { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Note { get; set; }
}

