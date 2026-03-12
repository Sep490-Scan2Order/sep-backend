using System.Collections.Generic;

namespace ScanToOrder.Application.DTOs.Orders;

public class CartModel
{
    public string CartId { get; set; } = string.Empty;
    public int RestaurantId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public string NumberPhone { get; set; } = null!;
    public List<CartItemModel> Items { get; set; } = new();
}

