using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Restaurant.Report
{
    public class OrderTypesDto
    {
        public OrderTypeStatsDto Regular { get; set; } = new OrderTypeStatsDto();
        public OrderTypeStatsDto Refund { get; set; } = new OrderTypeStatsDto();
    }
}
