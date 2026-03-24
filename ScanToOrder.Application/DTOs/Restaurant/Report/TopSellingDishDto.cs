using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Restaurant.Report
{
    public class TopSellingDishDto
    {
        public int DishId { get; set; }
        public string DishName { get; set; } = null!;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }
}
