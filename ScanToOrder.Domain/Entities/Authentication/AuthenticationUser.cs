using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScanToOrder.Domain.Entities.User;

namespace ScanToOrder.Domain.Entities.Authentication
{
    public class AuthenticationUser
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = null!;

        public string? Phone { get; set; }

        public string? Password { get; set; }

        public string Role { get; set; } = null!;

        public bool Verified { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual Tenant Tenant { get; set; } = null!;
    }
}
