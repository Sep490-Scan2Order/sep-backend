using System;

namespace ScanToOrder.Application.DTOs.Voucher
{
    public class RedeemVoucherResponseDto
    {
        public int MemberVoucherId { get; set; }
        public int VoucherId { get; set; }
        public string VoucherName { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public DateTime? ExpiredAt { get; set; }
    }
}
