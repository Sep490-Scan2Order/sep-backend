using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.SystemBlog
{
    public class SystemBlogDto
    {
        public int SystemBlogId { get; set; }
        public string Title { get; set; } = string.Empty;
        public new DateOnly CreatedAt { get; set; }
        public new DateOnly UpdatedAt { get; set; }
        public int TotalViews { get; set; }
        public BlogType BlogType { get; set; }
    }
}
