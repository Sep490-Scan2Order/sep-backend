namespace ScanToOrder.Application.DTOs.Payment;

public class PaymentStatusResponse
{
    public long OrderCode { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsFinal { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
