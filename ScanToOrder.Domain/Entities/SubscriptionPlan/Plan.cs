using System.ComponentModel.DataAnnotations.Schema;
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class Plan : BaseEntity<int>
    {
        public string Name { get; set; } = null!;
        public decimal MonthlyPrice { get; set; }
        public decimal YearlyPrice { get; set; }
        public int DurationInDays { get; set; }
        public decimal DailyRateMonth { get; set; }
        public decimal DailyRateYear { get; set; }
        public int Level { get; set; } = 0;
        public PlanStatus Status { get; set; }
        [Column(TypeName = "jsonb")]
        public PlanFeaturesConfig Features { get; set; } = new PlanFeaturesConfig();
        public virtual ICollection<SubscriptionLog> SubscriptionLogs { get; set; } = new List<SubscriptionLog>();
        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}
