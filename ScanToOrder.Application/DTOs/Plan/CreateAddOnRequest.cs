using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Plan
{
    public class CreateAddOnRequest
    {
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required decimal Price { get; set; }
        public int MaxDishesCount { get; set; }
        public int MaxCategoriesCount { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
