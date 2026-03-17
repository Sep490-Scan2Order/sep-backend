using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Dashboard
{
    public class SubscriptionPlanDistributionDto
    {
        public string PlanName { get; set; } = string.Empty;
        public double Percentage { get; set; }
        public int Count { get; set; }
    }
}
