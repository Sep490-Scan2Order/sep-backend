using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class SystemBlogRepository : GenericRepository<SystemBlog>, ISystemBlogRepository
    {
        private readonly AppDbContext _context;
        public SystemBlogRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
