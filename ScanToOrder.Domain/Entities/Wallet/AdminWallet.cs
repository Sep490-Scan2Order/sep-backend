using ScanToOrder.Domain.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.Wallet
{
    public class AdminWallet : BaseEntity<int>
    {
        public decimal VoucherBalance { get; set; }
        public decimal CommissionBalance { get; set; }
        public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    }
}
