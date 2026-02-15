using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Infrastructure.Configuration
{
    public class N8NSettings
    {
        public string TaxValidationUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}
