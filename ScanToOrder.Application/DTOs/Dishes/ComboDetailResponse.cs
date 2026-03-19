namespace ScanToOrder.Application.DTOs.Dishes
{
    public class ComboDetailResponse
    {
        public DishDto Dish { get; set; } = null!;
        public int Quantity { get; set; }
    }
}
