using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.DTOs.Menu
{
    public class MenuTemplateRenderDto
    {
        public int TemplateId { get; set; }
        public int RestaurantId { get; set; }

        public string? ThemeColor { get; set; }
        public string? FontFamily { get; set; }

        public string? LayoutConfigJson { get; set; }

        public List<MenuCategoryDto> MenuData { get; set; } = new();
    }
}

