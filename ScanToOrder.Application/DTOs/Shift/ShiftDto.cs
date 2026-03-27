using ScanToOrder.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Shift
{
    public class ShiftDto
    {
        public int Id { get; set; }
        public int RestaurantId { get; set; }
        public Guid StaffId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }


        public decimal OpeningCashAmount { get; set; }

        public string Note { get; set; } = string.Empty;

        public ShiftStatus Status { get; set; }
    }
}
