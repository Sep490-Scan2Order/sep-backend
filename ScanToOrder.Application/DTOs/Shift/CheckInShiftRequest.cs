using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Shift
{
    public class CheckInShiftRequest
    {
        public int RestaurantId { get; set; }
        public Guid StaffId { get; set; }
        public decimal OpeningCashAmount { get; set; }
        public string? Note { get; set; }
    }
}
