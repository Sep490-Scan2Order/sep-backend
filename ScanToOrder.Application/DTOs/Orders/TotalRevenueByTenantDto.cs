namespace ScanToOrder.Application.DTOs.Orders
{
    public class TotalRevenueByTenantDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public bool IsAllTime { get; set; }
        public string FilterPreset { get; set; } = "allTime";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalRestaurants { get; set; }
        public int TotalOrders { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<TenantRestaurantRevenueDto> Restaurants { get; set; } = new();
    }

    public class TenantRestaurantRevenueDto
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public string? Image { get; set; }
        public string? Address { get; set; }
        public string? CurrentPlan { get; set; }
        public bool IsActive { get; set; }
        public int TotalOrders { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal AverageOrderValue { get; set; }
    }
}
