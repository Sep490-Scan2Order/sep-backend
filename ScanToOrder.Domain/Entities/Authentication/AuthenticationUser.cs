using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.Authentication
{
    public class AuthenticationUser : BaseEntity<Guid>
    {

        public string Email { get; set; } = null!;

        public string? Phone { get; set; }

        public string? Password { get; set; }

        public Role Role { get; set; }

        public bool Verified { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual Tenant Tenant { get; set; } = null!;
        public virtual Staff Staff { get; set; } = null!;
        public virtual Customer Customer { get; set; } = null!;
    }
}
