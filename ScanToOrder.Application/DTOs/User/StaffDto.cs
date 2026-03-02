namespace ScanToOrder.Application.DTOs.User
{
    public class StaffDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;  
        public string Name { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
