using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.Orders
{
    public class Order : BaseEntity<Guid>
    {
        public Guid? UserId { get; set; }
        public int RestaurantId { get; set; }
        public int? PromotionId { get; set; }

        public int OrderCode { get; set; }

        public bool IsPreOrder { get; set; }
        public string? TableNumber { get; set; } 
        public string? Note { get; set; } 

        public decimal TotalAmount { get; set; }
        public decimal PromotionDiscount { get; set; }
        public decimal FinalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public bool IsScanned { get; set; } = false;

        public string Type { get; set; } = null!;

        public virtual AuthenticationUser? User { get; set; }

        public virtual Promotion? Promotion { get; set; }

        public virtual Restaurant.Restaurant Restaurant { get; set; } = null!;
    }
}
