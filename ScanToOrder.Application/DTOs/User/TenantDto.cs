namespace ScanToOrder.Application.DTOs.User
{
    public class TenantDto
    {
        public Guid Id { get; set; } 
        public string? Name { get; set; }
        
        public Guid AccountId { get; set; }
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Avatar { get; set; }
        public string Role { get; set; } = null!; 
        public bool Verified { get; set; }
        public bool IsActive { get; set; }

        public string? TaxNumber { get; set; }
        public Guid? BankId { get; set; }
        public string? CardNumber { get; set; }
        public string BankName { get; set; } = string.Empty; 
        public string BankLogo { get; set; } = string.Empty;
        public bool IsVerifyBank { get; set; }
        public bool IsVerifyTax { get; set; }
        
        public DateTime? DebtStartedAt { get; set; }
        public DateTime? SubscriptionExpiryDate { get; set; }
        public DateTime? LastWarningSentAt { get; set; }
        public decimal TotalDebtAmount { get; set; }
        public string PlanName { get; set; } = string.Empty;

        public int TotalRestaurants { get; set; }
        public int TotalDishes { get; set; }
        public int TotalCategories { get; set; }
    }
}
