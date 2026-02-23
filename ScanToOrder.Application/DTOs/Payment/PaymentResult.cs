namespace ScanToOrder.Application.DTOs.Payment;

public class PaymentResult
{
    public long OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; }
    public string CounterAccountName { get; set; }
    public string BankBin { get; set; }
    public string AccountNumber { get; set; }
    public string Description { get; set; }
    public bool IsPaymentSuccess { get; set; }
}