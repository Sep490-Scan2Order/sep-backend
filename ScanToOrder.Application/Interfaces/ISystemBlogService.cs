using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.Interfaces
{
    public interface ISystemBlogService
    {
        Task<(List<SystemBlogDto> Items, int TotalCount)> GetSystemBlogsAsync(
            int pageIndex, int pageSize, BlogType? blogType);
        Task<AddSystemBlogDtoResponse> AddSystemBlogAsync(AddSystemBlogDtoRequest request);
        Task<BlogDetailDto> GetSystemBlogByIdAsync(int systemBlogId);
    }
}
