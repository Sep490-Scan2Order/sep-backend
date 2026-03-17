using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Dashboard
{
    public class ExpiringSubscriptionDto
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public DateTime ExpirationDate { get; set; }
    }
}
