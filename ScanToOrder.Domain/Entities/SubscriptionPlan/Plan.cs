using ScanToOrder.Domain.Entities.Base;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class Plan : BaseEntity<int>
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public int DurationInDays { get; set; }
        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public bool IsActive { get; set; }
    }
}
