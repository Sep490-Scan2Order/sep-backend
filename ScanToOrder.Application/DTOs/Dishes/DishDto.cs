using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Dishes
{
    public class DishDto
    {   
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string DishName { get; set; } = null!;
        public decimal Price { get; set; }

        public string Description { get; set; } = null!;

        public string ImageUrl { get; set; } = null!;
        public int DishAvailability { get; set; } = 1;

        public bool IsAvailable { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
