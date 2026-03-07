using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Entities.Restaurants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.Shift
{
    public class Shift : BaseEntity<int>
    {
        public int RestaurantId { get; set; }
        public Guid StaffId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int OpeningCashAmount { get; set; }
        public string Note { get; set; } = string.Empty;
        public ShiftStatus Status { get; set; }
        public virtual Restaurant Restaurants { get; set; } = null!;
        public virtual Staff Staffs { get; set; } = null!;
    }
}
