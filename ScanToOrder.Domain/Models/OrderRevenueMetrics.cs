using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Models
{
    public class OrderRevenueMetrics
    {
        public int TotalOrders { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal TotalDiscount { get; set; }

        public int RegularCount { get; set; }
        public decimal RegularRevenue { get; set; }

        public int RefundCount { get; set; }
        public decimal RefundRevenue { get; set; }

        // Refund breakdown
        public int RefundObjectiveCount { get; set; }
        public decimal RefundObjectiveRevenue { get; set; }
        public int RefundStaffErrorCount { get; set; }
        public decimal RefundStaffErrorRevenue { get; set; }
        public int RefundSystemErrorCount { get; set; }
        public decimal RefundSystemErrorRevenue { get; set; }

        // Payment method breakdown
        public decimal TotalCash { get; set; }
        public decimal TotalTransfer { get; set; }
    }
}
