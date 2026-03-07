using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Base;

namespace ScanToOrder.Domain.Entities.User;

public partial class Staff : BaseEntity<Guid>
{
    public Guid AccountId { get; set; }

    public string Name { get; set; } = null!;


    public int RestaurantId { get; set; }

    public virtual Restaurant.Restaurant Restaurant { get; set; } = null!;
    public virtual ICollection<Shift.Shift> Shifts { get; set; } = new List<Shift.Shift>();
    public virtual AuthenticationUser Account { get; set; } = null!;

}
