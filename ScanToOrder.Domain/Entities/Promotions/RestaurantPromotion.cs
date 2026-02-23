using ScanToOrder.Domain.Entities.Base;

namespace ScanToOrder.Domain.Entities.Promotions;

public class RestaurantPromotion : BaseEntity<int>
{
    public int RestaurantId { get; set; }
    public int PromotionId { get; set; }

    public virtual Restaurant.Restaurant Restaurant { get; set; } = null!;
    public virtual Promotion Promotion { get; set; } = null!;
}
