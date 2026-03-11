using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class DishesRepository : GenericRepository<Dish>, IDishesRepository
    {
        public DishesRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<Dish>> GetAllDishesByTenant(Guid tenantId, bool includeDeleted = false)
        {
            var query = _dbSet.Include(d => d.Category)
                              .Where(d => d.Category.TenantId == tenantId);

            
            if (!includeDeleted)
            {
                query = query.Where(d => !d.IsDeleted);
            }

            return await query.ToListAsync();
        }

        public async Task<int> GetTotalDishesByTenant(Guid tenantId)
        {
            return await _dbSet
                 .Where(d => d.Category.TenantId == tenantId)
                 .CountAsync();
        }

        public async Task<List<Dish>> GetDishesByCategoryIdAsync(int categoryId)
        {
            return await _dbSet.Where(d => d.CategoryId == categoryId).ToListAsync();
        }
    }
}

