namespace ScanToOrder.Application.DTOs.Restaurant;

public class MenuCategoryDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<MenuDishItemDto> Dishes { get; set; } = new List<MenuDishItemDto>();
}