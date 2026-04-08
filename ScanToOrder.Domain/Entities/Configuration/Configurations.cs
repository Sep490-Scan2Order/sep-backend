using ScanToOrder.Domain.Entities.Base;

namespace ScanToOrder.Domain.Entities.Configuration
{
    public class Configurations : BaseEntity<int>
    {
        public int CommissionRate { get; set; }
    }
}
