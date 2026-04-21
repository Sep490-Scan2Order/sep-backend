namespace ScanToOrder.Application.DTOs.Shift
{
    public class ShiftTransferQrResponse
    {
        public string QrUrl { get; set; } = string.Empty;
        public string PaymentCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Note { get; set; }
    }
}
