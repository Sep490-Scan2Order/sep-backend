using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Shift
{
    public class CheckOutShiftRequest
    {
        public int ShiftId { get; set; }
        public decimal CashAmount { get; set; }
        
        public string? Note { get; set; }
    }
}
