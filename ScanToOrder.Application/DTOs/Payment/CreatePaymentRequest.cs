namespace ScanToOrder.Application.DTOs.Payment;

public class CreatePaymentRequest
{
    public long OrderCode { get; set; }
    public long Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}