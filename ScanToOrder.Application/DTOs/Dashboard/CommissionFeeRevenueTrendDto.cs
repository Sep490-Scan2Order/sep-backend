using System;

namespace ScanToOrder.Application.DTOs.Dashboard
{
    public class CommissionFeeRevenueTrendDto
    {
        public string Month { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }
}
