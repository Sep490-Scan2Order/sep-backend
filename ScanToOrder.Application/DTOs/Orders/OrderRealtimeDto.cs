using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class OrderRealtimeDto
    {
        public Guid Id { get; set; }
        public int OrderCode { get; set; }
        public string Phone { get; set; }
        public decimal TotalAmount { get; set; }
        public string Note { get; set; }
        public int Status { get; set; }
        public List<OrderItemRealtimeDto> Items { get; set; }
    }
}
