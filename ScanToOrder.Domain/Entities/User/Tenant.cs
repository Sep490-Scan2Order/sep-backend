using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Bank;
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Domain.Entities.User;

public partial class Tenant : BaseEntity<Guid>
{
    public Guid AccountId { get; set; }

    public string? TaxNumber { get; set; }

    public Guid? BankId { get; set; }

    public string? CardNumber { get; set; }

    public bool IsVerifyTax { get; set; } = false;
    public string? Name { get; set; }
    public int TotalRestaurants { get; set; }
    public int TotalDishes { get; set; }
    public int TotalCategories { get; set; }

    public virtual AuthenticationUser Account { get; set; } = null!;
    public virtual ICollection<Restaurant.Restaurant> Restaurants { get; set; } = new List<Restaurant.Restaurant>();
    public virtual ICollection<Category > Category { get; set; } = new List<Category>();
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public virtual Banks? Bank { get; set; }
}
