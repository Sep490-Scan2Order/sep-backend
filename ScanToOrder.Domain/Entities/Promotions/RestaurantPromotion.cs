using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Restaurants;

namespace ScanToOrder.Domain.Entities.Promotions;

public class RestaurantPromotion : BaseEntity<int>
{
    public int RestaurantId { get; set; }
    public int PromotionId { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;
    public virtual Promotion Promotion { get; set; } = null!;
}
