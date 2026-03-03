namespace ScanToOrder.Application.DTOs.Menu
{
    public class MenuRestaurantDto
    {
        public int RestaurantId { get; set; }
        public int MenuTemplateId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
