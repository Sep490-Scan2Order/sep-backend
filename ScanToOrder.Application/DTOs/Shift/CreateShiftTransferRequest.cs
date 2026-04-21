namespace ScanToOrder.Application.DTOs.Shift
{
    public class CreateShiftTransferRequest
    {
        public int ShiftId { get; set; }
        public decimal Amount { get; set; }
        public string? TransactionCode { get; set; }
        public string? Note { get; set; }
    }
}
