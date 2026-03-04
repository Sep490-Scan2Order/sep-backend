using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.DTOs.Menu
{
    public class MenuRestaurantDto
    {
        public int RestaurantId { get; set; }
        public int MenuTemplateId { get; set; }
        public RestaurantDto Restaurant { get; set; } = null!;
        public MenuTemplateDto MenuTemplate { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
