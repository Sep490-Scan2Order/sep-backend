using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class Plan : BaseEntity<int>
    {
        public string Name { get; set; } = null!;
        public int MonthlyPrice { get; set; }
        public int YearlyPrice { get; set; }

        public int DurationInDays { get; set; }

        public int DailyRateMonth { get; set; }

        public int DailyRateYear { get; set; }

        public PlanStatus Status { get; set; }

        public virtual ICollection<SubscriptionLog> SubscriptionLogs { get; set; } = new List<SubscriptionLog>();
        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}
