using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class DishesRepository : GenericRepository<Dish>, IDishesRepository
    {
        private readonly AppDbContext _context;
        public DishesRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<Dish>> GetAllDishesByTenant(Guid tenantId)
        {
            return await _dbSet.Include(d => d.Category)
                         .Where(d => d.Category.TenantId == tenantId && !d.IsDeleted)
                         .ToListAsync();
        }

        public async Task<int> GetTotalDishesByTenant(Guid tenantId)
        {
            return await _dbSet
                 .Where(d => d.Category.TenantId == tenantId && !d.IsDeleted)
                 .CountAsync();
        }
    }
}
