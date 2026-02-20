using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.User;

public partial class Tenant
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public string? TaxNumber { get; set; }

    public string? BankName { get; set; }

    public string? CardNumber { get; set; }

    public string? Phone { get; set; }

    public bool Status { get; set; } 
    public string? Name { get; set; }

    public int TotalRestaurants { get; set; }
    public int TotalDishes { get; set; }
    public int TotalCategories { get; set; }

    public virtual AuthenticationUser Account { get; set; } = null!;
    public virtual ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();

    public virtual ICollection<Category > Category { get; set; } = new List<Category>();

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
