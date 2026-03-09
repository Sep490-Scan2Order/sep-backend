using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Restaurant
{
    public class AssignPresentCashierDto
    {
        public Guid CashierId { get; set; }
        public string CashierName { get; set; }
    }
}
