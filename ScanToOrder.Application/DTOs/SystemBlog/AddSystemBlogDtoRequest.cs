using Microsoft.AspNetCore.Http;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.SystemBlog
{
    public class AddSystemBlogDtoRequest
    {
        public string Content { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ColorTitle { get; set; } = string.Empty;

        public List<IFormFile>? Images { get; set; }

        public BlogType BlogType { get; set; }
    }
}
