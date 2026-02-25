namespace ScanToOrder.Application.DTOs.User
{
    public class TenantDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public required string Name { get; set; }
        public required string Phone { get; set; }
        public required string TaxNumber { get; set; }
        public required string BankName { get; set; }
        public required string CardNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PlanName { get; set; } = "Chưa mua gói";
        public int TotalRestaurants { get; set; } = 0;
        public int TotalDishes { get; set; } = 0;
        public int TotalCategories { get; set; } = 0;
        public string BankLogo { get; set; } = string.Empty;
    }
}
