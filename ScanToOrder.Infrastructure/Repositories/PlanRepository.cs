using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class PlanRepository : GenericRepository<Plan>, IPlanRepository
    {
        public PlanRepository(AppDbContext context) : base(context)
        {
        }
        
        public async Task<Dictionary<int, Plan>> GetByIds (List<int> ids)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(r => ids.Contains(r.Id) && !r.IsDeleted)
                .ToDictionaryAsync(r => r.Id);
        }
    }
}

