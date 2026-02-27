namespace ScanToOrder.Application.DTOs.User
{
    public class AdminDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
