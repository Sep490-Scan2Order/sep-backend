using ScanToOrder.Domain.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.Dishes
{
    public class ComboDetail : BaseEntity<int>
    {
        public int DishId { get; set; }
        public int ItemDishId { get; set; }

        public int Quantity { get; set; }

        public virtual Dish Dish { get; set; } = null!;
        public virtual Dish ItemDish { get; set; } = null!;
    }
}
