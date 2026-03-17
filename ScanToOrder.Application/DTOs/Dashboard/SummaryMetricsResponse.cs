using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Dashboard
{
    public class SummaryMetricsResponse
    {
        public int TotalTenants { get; set; }
        public int TotalRestaurants { get; set; }
        public decimal PlatformRevenue { get; set; }
        public int ActiveAccounts { get; set; }
    }
}
