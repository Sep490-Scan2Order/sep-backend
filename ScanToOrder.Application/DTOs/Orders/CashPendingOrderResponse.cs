using System;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class CashPendingOrderResponse
    {
        public string Id { get; set; } = null!;
        public int OrderCode { get; set; }
        public int RestaurantId { get; set; }
        public DateTime CreatedAt { get; set; }
  
        public decimal Amount { get; set; }
        public string Phone { get; set; } = null!;
        public string? Note { get; set; }
        public int Status { get; set; }
        public string? Type { get; set; }
        public List<CashPendingOrderItem> Items { get; set; } = new();
    }
}

