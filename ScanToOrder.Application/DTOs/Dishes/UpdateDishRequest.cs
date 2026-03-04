using Microsoft.AspNetCore.Http;

namespace ScanToOrder.Application.DTOs.Dishes
{
    public class UpdateDishRequest
    {
        public string? DishName { get; set; }
        public decimal? Price { get; set; }
        public string? Description { get; set; }
        public IFormFile? ImageUrl { get; set; }
        public int? DishAvailability { get; set; }
    }
}
