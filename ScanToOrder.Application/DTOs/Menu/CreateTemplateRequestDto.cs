using Microsoft.AspNetCore.Http;

namespace ScanToOrder.Application.DTOs.Menu
{
    public class CreateTemplateRequestDto
    {
        public string TemplateName { get; set; } = null!;
        public string? LayoutConfigJson { get; set; }
        public string? ThemeColor { get; set; }
        public string? FontFamily { get; set; }
        public IFormFile? BackgroundImageUrl { get; set; }
    }
}
