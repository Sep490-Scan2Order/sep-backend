using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Interfaces
{
    public interface ISystemBlogRepository : IGenericRepository<SystemBlog>
    {
        Task<(List<SystemBlog> Items, int TotalCount)> GetSystemBlogsSortByCreatedDateAsync(int pageIndex, int pageSize, BlogType? blogType);
    }
}
