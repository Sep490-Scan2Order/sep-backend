using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.Wallet
{
    public class WalletTransaction : BaseEntity<int>
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public Guid AdminId { get; set; }
        public int SubsciptionId { get; set; }
        public Subscription Subscription { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; } 
        public decimal BalanceAfter { get; set; } 
        public long OrderCode { get; set; }
        public TransactionStatus TransactionStatus { get; set; } = TransactionStatus.Pending;
        public WalletType WalletType { get; set; }
        public TransactionType TransactionType { get; set; }
        public NoteWalletTransaction? Note { get; set; }
    }
}
