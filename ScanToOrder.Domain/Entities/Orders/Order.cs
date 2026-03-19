using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.Orders
{
    public class Order : BaseEntity<Guid>
    {
        public int RestaurantId { get; set; }
        public int? PromotionId { get; set; }
        
        public Guid? RefundOrderId { get; set; }
        public Guid? ResponsibleStaffId { get; set; }
        public string NumberPhone { get; set; } = null!;
        public int OrderCode { get; set; }
        public string QrCodeUrl { get; set; } = null!;
        public bool IsPreOrder { get; set; }
        public string? Note { get; set; } 

        public decimal TotalAmount { get; set; }
        public decimal PromotionDiscount { get; set; }
        public decimal FinalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public bool IsScanned { get; set; } = false;

        public TypeOrder typeOrder { get; set; }
        public RefundType? RefundType { get; set; }
        public string? PaymentProofUrl { get; set; }

        public string Type { get; set; } = null!;

        public virtual Promotion? Promotion { get; set; }

        public virtual Restaurant Restaurant { get; set; } = null!;
        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}
