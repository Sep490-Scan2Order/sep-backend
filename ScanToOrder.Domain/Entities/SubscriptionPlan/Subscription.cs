using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class Subscription : BaseEntity
    {
        public Guid TenantId { get; set; }
        public virtual Tenant Tenant { get; set; } = null!;
        public int PlanId { get; set; }
        public virtual Plan Plan { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
    }
}
