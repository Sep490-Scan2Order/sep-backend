namespace ScanToOrder.Application.DTOs.Orders
{
    public class CustomerOrderDetailDto
    {
        public int DishId { get; set; }
        public string DishName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountedPrice { get; set; }
        public decimal SubTotal { get; set; }
    }
}

