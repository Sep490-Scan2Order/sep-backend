using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;

namespace ScanToOrder.Domain.Entities.Dishes
{
    public partial class Category : BaseEntity<int>
    {
        public Guid TenantId { get; set; }
        public string CategoryName { get; set; } = null!;
        public bool? IsActive { get; set; }
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual ICollection<Dish> Dishes { get; set; } = new List<Dish>();
    }
}
