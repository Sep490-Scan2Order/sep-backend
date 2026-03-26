using ScanToOrder.Application.DTOs.Restaurant.Report;

namespace ScanToOrder.Application.DTOs.Dashboard
{
    public class TenantDetailDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public bool IsSuspended { get; set; }
        public PeriodDto Period { get; set; } = new();
        public List<RestaurantRevenueDto> Restaurants { get; set; } = new();
    }

    public class RestaurantRevenueDto
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
    }
}
