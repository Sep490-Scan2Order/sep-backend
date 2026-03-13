namespace ScanToOrder.Application.DTOs.Orders;

public class CartItemModel
{
    public int DishId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal DiscountedPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal PromotionAmount { get; set; }
    public string? PromotionName { get; set; }
    public decimal SubTotal { get; set; }
}

