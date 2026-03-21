namespace ScanToOrder.Application.DTOs.Shift
{
    public class ShiftReportDto
    {
        public int Id { get; set; }
        public int ShiftId { get; set; }
        public DateTime ReportDate { get; set; }

        public decimal TotalCashOrder { get; set; }
        public decimal TotalTransferOrder { get; set; }
        public decimal TotalRefundAmount { get; set; }

        public decimal ExpectedCashAmount { get; set; }
        public decimal ActualCashAmount { get; set; }
        public decimal Difference { get; set; }

        public decimal ExpectedTotalAmount { get; set; }

        public string Note { get; set; } = string.Empty;
    }
}
