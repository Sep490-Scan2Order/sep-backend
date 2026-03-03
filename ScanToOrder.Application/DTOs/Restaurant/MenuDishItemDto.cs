namespace ScanToOrder.Application.DTOs.Restaurant;

public class MenuDishItemDto
{
    public int DishId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Price { get; set; } 
    public bool IsSoldOut { get; set; }
}