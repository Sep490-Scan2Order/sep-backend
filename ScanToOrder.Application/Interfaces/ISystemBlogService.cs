using ScanToOrder.Application.DTOs.SystemBlog;
using ScanToOrder.Domain.Entities.Blogs;

namespace ScanToOrder.Application.Interfaces
{
    public interface ISystemBlogService
    {
        Task<IEnumerable<SystemBlog>> GetSystemBlogAsync();
        Task<AddSystemBlogDtoResponse> AddSystemBlogAsync(AddSystemBlogDtoRequest request);
    }
}
