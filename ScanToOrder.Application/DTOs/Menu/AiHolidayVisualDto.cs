using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Menu
{
    public class AiHolidayVisualDto
    {
        public string TemplateName { get; set; } = null!;
        public string ThemeColor { get; set; } = null!;
        public string FontFamily { get; set; } = null!;
        public string BackgroundColor { get; set; } = null!;
        public string BackgroundImagePrompt { get; set; } = null!;
        public string LayoutConfigJson { get; set; } = null!;
    }
}
