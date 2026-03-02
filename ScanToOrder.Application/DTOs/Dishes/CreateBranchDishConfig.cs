using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Dishes
{
    public class CreateBranchDishConfig
    {
        public int RestaurantId { get; set; }
        public int DishId { get; set; }
        public bool IsSelling { get; set; } = true;
        public decimal Price { get; set; }
        public bool IsSoldOut { get; set; } = false;
    }
}
