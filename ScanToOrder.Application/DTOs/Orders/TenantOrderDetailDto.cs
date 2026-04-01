namespace ScanToOrder.Application.DTOs.Orders
{
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
