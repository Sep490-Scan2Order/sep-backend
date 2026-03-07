using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class Subscription : BaseEntity<int>
    {
        public int PlanId { get; set; }
        public int RestaurantId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SubscriptionStatus Status { get; set; }
        public virtual Plan Plan { get; set; } = null!;
        public virtual Restaurant.Restaurant Restaurant { get; set; } = null!;
    }
}
