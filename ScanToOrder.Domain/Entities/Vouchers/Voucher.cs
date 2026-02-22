using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.Vouchers
{
    public class Voucher : BaseEntity<int>
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public int PointRequire { get; set; }
        public VoucherStatus Status { get; set; }

        public virtual ICollection<MemberVoucher> MemberVouchers { get; set; } = new List<MemberVoucher>();
    }
}
