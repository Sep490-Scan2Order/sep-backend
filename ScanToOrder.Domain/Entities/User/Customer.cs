using ScanToOrder.Domain.Entities.Authentication;

namespace ScanToOrder.Domain.Entities.User;

public partial class Customer
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public DateOnly? Dob { get; set; }

    public virtual AuthenticationUser Account { get; set; } = null!;
}
