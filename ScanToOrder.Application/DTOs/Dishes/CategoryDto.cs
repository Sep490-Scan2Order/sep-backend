namespace ScanToOrder.Application.DTOs.Dishes
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public Guid TenantId { get; set; }
        public string CategoryName { get; set; } = null!;
        public bool? IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
