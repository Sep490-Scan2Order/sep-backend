using ScanToOrder.Application.DTOs.SystemBlog;

namespace ScanToOrder.Application.Interfaces
{
    public interface ISystemBlogService
    {
        Task<IEnumerable<SystemBlogDto>> GetSystemBlogAsync();
        Task<AddSystemBlogDtoResponse> AddSystemBlogAsync(AddSystemBlogDtoRequest request);
        Task<BlogDetailDto> GetSystemBlogByIdAsync(int systemBlogId);
    }
}
