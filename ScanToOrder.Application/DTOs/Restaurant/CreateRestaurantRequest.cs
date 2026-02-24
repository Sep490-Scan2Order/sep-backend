using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Restaurant
{
    public class CreateRestaurantRequest
    {
        public string RestaurantName { get; set; } = null!;
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Image { get; set; }
        public string? Phone { get; set; }
        public string? Description { get; set; }
    }
}
