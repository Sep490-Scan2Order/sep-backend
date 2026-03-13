using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Domain.Entities.Orders
{
    public class OrderDetail : BaseEntity<int>
    {
        public Guid OrderId { get; set; }
        public int DishId { get; set; }
        public int Quantity { get; set; }     
        public decimal SubTotal { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountedPrice { get; set; }
        public decimal PromotionAmount { get; set; }

        public virtual Order Order { get; set; } = null!;
        public virtual Dish Dish { get; set; } = null!;
    }
}
