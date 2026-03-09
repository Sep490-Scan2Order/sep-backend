namespace ScanToOrder.Application.DTOs.Menu
{
    public class MenuTemplateDto
    {
        public int Id { get; set; }
        public string TemplateName { get; set; } = null!;
        public string? LayoutConfigJson { get; set; }
        public string? ThemeColor { get; set; }
        public string? FontFamily { get; set; }
        public string? BackgroundImageUrl { get; set; }
    }
}
