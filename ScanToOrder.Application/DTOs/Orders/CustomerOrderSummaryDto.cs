using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class CustomerOrderSummaryDto
    {
        public Guid OrderId { get; set; }
        public int OrderCode { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public decimal FinalAmount { get; set; }
        public string QrCodeUrl { get; set; } = string.Empty;
        public List<CustomerOrderDetailDto> OrderDetails { get; set; } = new();
    }
}

