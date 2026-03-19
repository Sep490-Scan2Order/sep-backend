using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.Restaurant;

public class MenuDishItemDto
{
    public int DishId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Price { get; set; } 
    public bool IsSoldOut { get; set; }
    public bool IsSelling { get; set; }
    public int DiscountedPrice { get; set; }
    public string? PromotionName { get; set; }
    public string? PromotionLabel { get; set; }
    public DateTime? ExpiredAt { get; set; } 
    public PromotionType? PromoType { get; set; } 
    public DishType Type { get; set; }
    public int DishAvailabilityStock { get; set; }
    public bool HasPromotion => DiscountedPrice < Price;
}