using ScanToOrder.Domain.Entities.Base;
using System;
using System.Collections.Generic;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.Dishes
{
    public partial class Dish : BaseEntity<int>
    {
        public int CategoryId { get; set; }
        public string DishName { get; set; } = null!;
        public decimal Price { get; set; }
        public DishType Type { get; set; }
        public string Description { get; set; } = null!;

        public string ImageUrl { get; set; } = null!;

        public bool IsAvailable { get; set; }

        public virtual Category Category { get; set; } = null!;
        public virtual ICollection<BranchDishConfig> BranchDishConfigs { get; set; } = new List<BranchDishConfig>();
        public virtual ICollection<PromotionDish> PromotionDishes { get; set; } = new List<PromotionDish>();

        public virtual ICollection<ComboDetail> ComboDetails { get; set; } = new List<ComboDetail>();
    }
}
