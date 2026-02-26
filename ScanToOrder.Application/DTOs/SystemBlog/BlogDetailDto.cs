using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.SystemBlog
{
    public class BlogDetailDto
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ColorTitle { get; set; } = string.Empty;
        public new DateOnly CreatedAt { get; set; }
        public new DateOnly UpdatedAt { get; set; }
        public int TotalViews { get; set; }
        public BlogType BlogType { get; set; }
        public string ImageUrl { get; set; } = "[]";
    }
}
