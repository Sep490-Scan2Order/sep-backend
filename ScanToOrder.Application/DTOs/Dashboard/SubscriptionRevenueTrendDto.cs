using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Dashboard
{
    public class SubscriptionRevenueTrendDto
    {
        public string Month { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }
}
