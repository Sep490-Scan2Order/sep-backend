using Microsoft.AspNetCore.Http;

namespace ScanToOrder.Application.DTOs.Dishes
{
    public class CreateDishRequest
    {
        public string DishName { get; set; } = null!;
        public decimal Price { get; set; }

        public string Description { get; set; } = null!;

        public IFormFile ImageUrl { get; set; } = null!;
        public int DishAvailability { get; set; } = 1;
    }
}
