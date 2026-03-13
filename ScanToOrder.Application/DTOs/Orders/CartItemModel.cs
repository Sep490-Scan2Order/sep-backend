namespace ScanToOrder.Application.DTOs.Orders;

public class CartItemModel
{
    public int DishId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromotionName { get; set; }
    public decimal SubTotal { get; set; }
}

