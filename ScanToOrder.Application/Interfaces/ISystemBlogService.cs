using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Blogs;

namespace ScanToOrder.Application.Interfaces
{
    public interface ISystemBlogService
    {
        Task<ApiResponse<IEnumerable<SystemBlog>>> GetSystemBlogAsync();
        Task<ApiResponse<AddSystemBlogDtoResponse>> AddSystemBlogAsync(AddSystemBlogDtoRequest request);
    }
}
