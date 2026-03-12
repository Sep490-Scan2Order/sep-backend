namespace ScanToOrder.Application.DTOs.Orders;

public class CashCheckoutRequest
{
    public string CartId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

