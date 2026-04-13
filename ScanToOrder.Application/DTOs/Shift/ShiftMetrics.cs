using System;

namespace ScanToOrder.Application.DTOs.Shift
{
    public class ShiftMetrics
    {
        public decimal TotalCashOrder { get; set; }
        public decimal TotalTransferOrder { get; set; }
        public decimal TotalRefundAmount { get; set; }
    }
}
