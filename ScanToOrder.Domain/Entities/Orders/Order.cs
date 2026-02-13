using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.Vouchers;

namespace ScanToOrder.Domain.Entities.Orders
{
    public class Order : BaseEntity<Guid>
    {
        public int? MemberVoucherId { get; set; }
        public Guid? UserId { get; set; }
        public int RestaurantId { get; set; }
        public int? PromotionId { get; set; }

        public int OrderCode { get; set; }

        public bool IsPreOrder { get; set; }
        public string? TableNumber { get; set; } 
        public string? Note { get; set; } 

        public decimal TotalAmount { get; set; }
        public decimal VoucherDiscount { get; set; }
        public decimal PromotionDiscount { get; set; }
        public decimal FinalAmount { get; set; }

        public virtual AuthenticationUser? User { get; set; }

        public virtual MemberVoucher? MemberVoucher { get; set; }

        public virtual Promotion? Promotion { get; set; }

        public virtual Restaurant Restaurant { get; set; } = null!;
    }
}
