using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class KdsItemResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal DiscountedPrice { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal PromotionAmount { get; set; }

        public int Quantity { get; set; }
        public string Image { get; set; }
    }
}
