using ScanToOrder.Domain.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.MemberVoucher
{
    public class MemberVoucher : BaseEntity
    {
        public Guid UserId { get; set; } 
        public int VoucherId { get; set; }

        public bool IsUsed { get; set; } = false;
        public DateTime? UsedAt { get; set; }

        public DateTime? ExpiredAt { get; set; }

        public virtual Voucher Voucher { get; set; } = null!;

        public bool IsExpired(DateTime now)
        {
            if (ExpiredAt.HasValue)
                return now > ExpiredAt.Value;

            return false;
        }

        public void MarkAsUsed(DateTime now)
        {
            if (IsUsed) throw new InvalidOperationException("Voucher này đã được sử dụng.");
            if (IsExpired(now)) throw new InvalidOperationException("Voucher đã hết hạn.");

            IsUsed = true;
            UsedAt = now;
        }
    }
}
