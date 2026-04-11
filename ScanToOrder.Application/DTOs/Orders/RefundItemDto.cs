namespace ScanToOrder.Application.DTOs.Orders
{
    public class RefundItemDto
    {
        public int OrderDetailId { get; set; }
        public int QuantityToRefund { get; set; }
    }
}
