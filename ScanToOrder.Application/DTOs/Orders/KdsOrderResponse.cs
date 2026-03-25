using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class KdsOrderResponse
    {
        public string Id { get; set; }
        public string Phone { get; set; }
        public int OrderCode { get; set; }

        public int RestaurantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RequestedPickupAt { get; set; }
        public DateTime? ConfirmedPickupAt { get; set; }
        public decimal Amount { get; set; }
        public bool IsPreOrder { get; set; }

        public int Status { get; set; }
        public string? Type { get; set; }
        public List<KdsItemResponse> Items { get; set; }
    }
}
