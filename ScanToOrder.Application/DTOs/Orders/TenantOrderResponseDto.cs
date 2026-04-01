using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class TenantOrderResponseDto
    {
        public Guid Id { get; set; }
        public int OrderCode { get; set; }
        public string NumberPhone { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public decimal PromotionDiscount { get; set; }
        public decimal FinalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public bool IsPreOrder { get; set; }
        public DateTime? RequestedPickupAt { get; set; }
        public DateTime? ConfirmedPickupAt { get; set; }
        public string? Note { get; set; }
        public string Type { get; set; } = null!;
        public string? PaymentProofUrl { get; set; }
        public TypeOrder TypeOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<TenantOrderDetailDto> OrderDetails { get; set; } = new List<TenantOrderDetailDto>();
    }

    public class TenantOrderDetailDto
    {
        public int DishId { get; set; }
        public string DishName { get; set; } = null!;
        public int Quantity { get; set; }     
        public decimal SubTotal { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountedPrice { get; set; }
        public decimal PromotionAmount { get; set; }
    }
}
