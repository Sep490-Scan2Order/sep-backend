using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScanToOrder.Domain.Entities.Restaurants;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class SubscriptionLog : BaseEntity<int>
    {
        public int RestaurantId { get; set; }   
        public int PaymentTransactionId { get; set; }
        public int OldPlanId { get; set; }
        public int NewPlanId { get; set; }
        public SubscriptionLogStatus ActionType { get; set; }
        public decimal AmountAllocated { get; set; }

        public decimal BalanceConvereted { get; set; }

        public int DaysAdded { get; set; }

        public DateTime OldExpired { get; set; }

        public DateTime NewExpired { get; set; }

        public Restaurant Restaurants { get; set; } = null!;

        public PaymentTransaction PaymentTransactions { get; set; } = null!;

        public Plan Plans { get; set; } = null!;
    }
}
