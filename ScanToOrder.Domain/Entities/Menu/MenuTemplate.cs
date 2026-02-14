using ScanToOrder.Domain.Entities.Base;

namespace ScanToOrder.Domain.Entities.Menu;

public class MenuTemplate : BaseEntity<int>
{
    public string TemplateName { get; set; } = null!;
    public string? LayoutConfigJson { get; set; }
    public string? ThemeColor { get; set; }
    public string? FontFamily { get; set; }

    public virtual ICollection<MenuRestaurant> MenuRestaurants { get; set; } = new List<MenuRestaurant>();
}
