namespace ScanToOrder.Application.DTOs.Menu
{
    public class UpdateMenuTemplateDto
    {
        public string TemplateName { get; set; } = null!;
        public string? ThemeColor { get; set; }
        public string? FontFamily { get; set; }
        public bool IsActive { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string? LayoutConfigJson { get; set; }
    }
}

