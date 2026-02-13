using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Points;

namespace ScanToOrder.Domain.Entities.User;

public partial class Customer
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public DateOnly? Dob { get; set; }
    public string Name { get; set; } = null!;

    public virtual AuthenticationUser Account { get; set; } = null!;
    public virtual ICollection<MemberPoint> MemberPoints { get; set; } = new List<MemberPoint>();
}
