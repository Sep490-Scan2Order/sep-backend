using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Plan
{
    public  class AddOnDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public int MaxDishesCount { get; set; }
        public int MaxCategoriesCount { get; set; }
    }
}
