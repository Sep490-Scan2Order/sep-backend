using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class SystemBlogRepository : GenericRepository<SystemBlog>, ISystemBlogRepository
    {
        public SystemBlogRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<(List<SystemBlog> Items, int TotalCount)> GetSystemBlogsSortByCreatedDateAsync(int pageIndex, int pageSize, BlogType? blogType)
        {
            var actualPageIndex = pageIndex <= 0 ? 1 : pageIndex;
            var actualPageSize = pageSize <= 0 ? 20 : pageSize;
            var offset = (actualPageIndex - 1) * actualPageSize;

            var query = _dbSet
                .Where(r => r.IsActive == true && r.IsDeleted == false);

            if (blogType.HasValue)
            {
                query = query.Where(r => r.BlogType == blogType.Value);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip(offset)
                .Take(actualPageSize)
                .ToListAsync();
            return (items, totalCount);
        }
    }
}

