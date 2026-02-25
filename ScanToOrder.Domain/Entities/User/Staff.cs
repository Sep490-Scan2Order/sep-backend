using ScanToOrder.Domain.Entities.Authentication;

namespace ScanToOrder.Domain.Entities.User;

public partial class Staff
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public int RestaurantId { get; set; }
    public string Name { get; set; } = null!;

    public virtual AuthenticationUser Account { get; set; } = null!;

    public virtual Restaurant.Restaurant Restaurant { get; set; } = null!;
}
