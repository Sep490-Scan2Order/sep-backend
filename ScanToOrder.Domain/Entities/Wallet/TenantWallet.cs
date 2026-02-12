using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.Wallet
{
    public class TenantWallet : BaseEntity 
    {
        public Guid TenantId { get; set; }
        public virtual Tenant Tenant { get; set; } = null!;
        public decimal WalletBalance { get; set; }
        public bool IsBlocked { get; set; } = false;
        public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>(); 
    }
}
