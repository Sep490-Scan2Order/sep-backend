using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Dishes
{
    public class BranchDishConfigDto
    {
        public int Id { get; set; }
        public required string RestaurantName { get; set; }
        public required string DishName { get; set; }

        public required string DishImageUrl { get; set; }
        public bool IsSelling { get; set; } = true;
        public decimal Price { get; set; }
        public bool IsSoldOut { get; set; } = false;
    }
}
