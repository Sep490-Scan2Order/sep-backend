using Microsoft.AspNetCore.Http;

namespace ScanToOrder.Application.DTOs.Restaurant
{
    public class UpdateRestaurantRequest
    {
        public string RestaurantName { get; set; } = null!;
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public IFormFile? Image { get; set; }
        public string? Phone { get; set; }
        public string? Description { get; set; }
    }
}

