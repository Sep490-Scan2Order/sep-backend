using ScanToOrder.Domain.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class AddOn : BaseEntity<int>
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public bool IsActive { get; set; }
        public int MaxDishesCount { get; set; }
        public int MaxCategoriesCount { get; set; }
    }
}
