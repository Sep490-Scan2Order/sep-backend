namespace ScanToOrder.Application.DTOs.Dishes
{
    public class UpdateDishRequest
    {
        public string DishName { get; set; } = null!;
        public decimal Price { get; set; }
        public string Description { get; set; } = null!;
        public string ImageUrl { get; set; } = null!;
        public int DishAvailability { get; set; } = 1;
    }
}
