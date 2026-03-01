using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
    {
        private readonly AppDbContext _context;
        public CategoryRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<Category>> GetAllCategoriesByTenant(Guid tenantId)
        {
            return await _dbSet.Where(c => c.TenantId == tenantId && !c.IsDeleted).ToListAsync();
        }

        public async Task<int> GetTotalCategoriesByTenant(Guid tenantId)
        {
            return await _dbSet.CountAsync(c => c.TenantId == tenantId && !c.IsDeleted);
        }
    }
}
