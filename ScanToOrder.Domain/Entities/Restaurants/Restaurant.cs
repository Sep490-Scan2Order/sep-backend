using NetTopologySuite.Geometries;
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;

namespace ScanToOrder.Domain.Entities.Restaurants;

public class Restaurant : BaseEntity<int>
{
    public Guid TenantId { get; set; }

    public string RestaurantName { get; set; } = null!;

    public string? Address { get; set; }
    public Point? Location { get; set; }

    public string? Image { get; set; }

    public string? Phone { get; set; }

    public string? Description { get; set; }

    public string? ProfileUrl { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }

    public string? QrMenu { get; set; }

    public bool? IsActive { get; set; }
    public required string Slug { get; set; }

    public bool? IsOpened { get; set; }

    public bool? IsReceivingOrders { get; set; }

    public int? TotalOrder { get; set; }

    public decimal MinCashAmount { get; set; }

    public bool IsAvailableShift { get; set; }

    public Guid? PresentCashierId { get; set; }

    public Pgvector.Vector? SearchVector { get; set; }

    public virtual ICollection<Staff> Staff { get; set; } = new List<Staff>();

    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public virtual ICollection<BranchDishConfig> BranchDishConfigs { get; set; } = new List<BranchDishConfig>();
    public virtual Subscription? Subscription { get; set; }
}
