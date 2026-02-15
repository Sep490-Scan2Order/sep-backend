using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.External
{
    public class TaxValidationResponse
    {
        public string status { get; set; } = null!;
        public string taxCode { get; set; } = null!;
        public string taxStatus { get; set; } = null!;
        public string fullName { get; set; } = null!;
    }
}
