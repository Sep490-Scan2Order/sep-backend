using ScanToOrder.Domain.Entities.Base;

namespace ScanToOrder.Domain.Entities.Menu;

public class MenuRestaurant : BaseEntity<int>
{
    public int RestaurantId { get; set; }
    public int MenuTemplateId { get; set; }

    public virtual Restaurant.Restaurant Restaurant { get; set; } = null!;
    public virtual MenuTemplate MenuTemplate { get; set; } = null!;
}
