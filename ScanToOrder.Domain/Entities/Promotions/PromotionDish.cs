using System.ComponentModel.DataAnnotations;
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Domain.Entities.Promotions
{
    public class PromotionDish : BaseEntity<int>
    {
        public int DishId { get; set; }
        public int PromotionId { get; set; }

        public virtual Dish Dish { get; set; } = null!;
        public virtual Promotion Promotion { get; set; } = null!;
    }
}
