using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.Wallet
{
    public class WalletTransaction : BaseEntity<int>
    {
        public int? TenantWalletId { get; set; }
        public virtual TenantWallet TenantWallet { get; set; } = null!;
        
        public int? AdminWalletId { get; set; }
        public virtual AdminWallet AdminWallet { get; set; } = null!;
        
        public int? SubscriptionId { get; set; }
        public virtual Subscription Subscription { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; } 
        public decimal BalanceAfter { get; set; } 
        public long OrderCode { get; set; }
        public DateTime PaymentDate { get; set; }
        public TransactionStatus TransactionStatus { get; set; } = TransactionStatus.Pending;
        public WalletType WalletType { get; set; }
        public TransactionType TransactionType { get; set; }
        public NoteWalletTransaction? Note { get; set; }
    }
}
