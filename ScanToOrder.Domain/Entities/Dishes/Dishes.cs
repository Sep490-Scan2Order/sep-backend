using ScanToOrder.Domain.Entities.Base;
using System;
using System.Collections.Generic;
namespace ScanToOrder.Domain.Entities.Dishes
{
    public partial class Dish : BaseEntity<int>
    {
        public int CategoryId { get; set; }
        public string DishName { get; set; } = null!;
        public decimal Price { get; set; }

        public string Description { get; set; } = null!;

        public string ImageUrl { get; set; } = null!;
        public int DishAvailability { get; set; } = 1;

        public bool IsAvailable { get; set; }

        public virtual Category Category { get; set; } = null!;
        public virtual ICollection<BranchDishConfig> BranchDishConfig { get; set; } = new List<BranchDishConfig>();
    }
}
