using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class CustomerOrderSummaryDto
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public Guid OrderId { get; set; }
        public int OrderCode { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public decimal FinalAmount { get; set; }
        public string QrCodeUrl { get; set; } = string.Empty;

        public TypeOrder TypeOrder { get; set; }
        public RefundType? RefundType { get; set; }
        public Guid? RefundOrderId { get; set; }

        public bool IsPreOrder { get; set; }
        public DateTime? RequestedPickupAt { get; set; }

        public bool IsRefundLog { get; set; }

        public List<CustomerOrderDetailDto> OrderDetails { get; set; } = new();
    }
}

