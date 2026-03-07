using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Restaurants;

namespace ScanToOrder.Domain.Entities.Menu;

public class MenuRestaurant : BaseEntity<int>
{
    public int RestaurantId { get; set; }
    public int MenuTemplateId { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;
    public virtual MenuTemplate MenuTemplate { get; set; } = null!;
}
