using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Restaurants;

namespace ScanToOrder.Domain.Entities.User;

public partial class Tenant
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public string? TaxNumber { get; set; }

    public string? BankName { get; set; }

    public string? CardNumber { get; set; }

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;
    public string? Name { get; set; }

    public virtual AuthenticationUser Account { get; set; } = null!;
    public virtual ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();
}
