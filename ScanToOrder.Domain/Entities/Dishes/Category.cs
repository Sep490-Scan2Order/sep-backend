using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.Dishes
{
    public partial class Category : BaseEntity
    {
        public Guid TenantId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public bool? IsActive { get; set; }
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual ICollection<Dishes> Dishes { get; set; } = new List<Dishes>();
    }
}
