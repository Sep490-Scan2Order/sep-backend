namespace ScanToOrder.Application.DTOs.Orders;

public class PaymentQrRequest
{
    public string CartId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

