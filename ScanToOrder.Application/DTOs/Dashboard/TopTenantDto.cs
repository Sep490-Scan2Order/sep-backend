namespace ScanToOrder.Application.DTOs.Dashboard
{
    public class TopTenantDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public int TotalRestaurants { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
