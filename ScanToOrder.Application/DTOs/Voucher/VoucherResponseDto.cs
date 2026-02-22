using ScanToOrder.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Voucher
{
    public class VoucherResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }

        public int PointRequire { get; set; }
        public VoucherStatus Status { get; set; }
    }
}
