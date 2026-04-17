using System;

namespace ScanToOrder.Application.DTOs.Dashboard
{
    public class SubscriptionRevenueByPlanDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public double Percentage { get; set; }
    }
}
