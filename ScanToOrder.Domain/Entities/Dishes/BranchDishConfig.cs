using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Restaurants;

namespace ScanToOrder.Domain.Entities.Dishes
{
    public partial class BranchDishConfig : BaseEntity<int>
    {
        public int RestaurantId { get; set; }
        public int DishId { get; set; }
        public bool IsSelling { get; set; } = true;
        public decimal Price { get; set; }
        public int DishAvailability { get; set; } = 1;
        public bool IsSoldOut { get; set; } = false;
        public virtual Restaurant Restaurant { get; set; } = null!;
        public virtual Dish Dish { get; set; } = null!;
    }
}
