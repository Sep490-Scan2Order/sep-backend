using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.SystemBlog
{
    public class AddSystemBlogDtoResponse
    {
        public int SystemBlogId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ColorTitle { get; set; } = string.Empty;
        public new DateOnly CreatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public new DateOnly UpdatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public int TotalViews { get; set; }

        public string ImageUrl { get; set; } = "[]";
        public BlogType BlogType { get; set; }
    }
}
