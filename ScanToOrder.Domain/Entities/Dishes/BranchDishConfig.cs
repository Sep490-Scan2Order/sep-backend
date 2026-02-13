using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Restaurants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.Dishes
{
    public partial class BranchDishConfig : BaseEntity<int>
    {
        public int RestaurantId { get; set; }
        public int DishId { get; set; }
        public bool IsSelling { get; set; } = true;
        public int Price { get; set; }
        public bool IsSoldOut { get; set; } = false;
        public virtual Restaurant Restaurant { get; set; } = null!;
        public virtual Dish Dish { get; set; } = null!;
    }
}
