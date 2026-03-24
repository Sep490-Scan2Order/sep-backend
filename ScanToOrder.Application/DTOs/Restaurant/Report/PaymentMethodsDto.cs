using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Restaurant.Report
{
    public class PaymentMethodsDto
    {
        public decimal Cash { get; set; }
        public decimal Transfer { get; set; }
    }
}
