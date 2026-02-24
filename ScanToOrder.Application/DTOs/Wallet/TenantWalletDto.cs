using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Wallet
{
    public class TenantWalletDto
    {
        public int Id { get; set; }
        public Guid TenantId { get; set; }
        public decimal WalletBalance { get; set; }
        public bool IsBlocked { get; set; }
    }
}
