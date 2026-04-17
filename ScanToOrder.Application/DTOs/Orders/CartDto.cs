using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.DTOs.Orders;

public class CartDto
{
    public string CartId { get; set; } = string.Empty;
    public int RestaurantId { get; set; }
    public decimal TotalAmount { get; set; }
    public List<CartItemModel> Items { get; set; } = new();
    public List<MenuDishItemDto>? Recommendations { get; set; } = new();
}

