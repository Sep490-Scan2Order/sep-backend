namespace ScanToOrder.Application.DTOs.Orders;

public class PaymentQrDto
{
    public Guid OrderId { get; set; }
    public string QrUrl { get; set; } = string.Empty;
    public string PaymentCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = null!;
}
