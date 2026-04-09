using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class TenantRepository : GenericRepository<Tenant>, ITenantRepository
    {
        public TenantRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<Tenant>> GetTenantsWithSubscriptionsAsync()
        {
            return await _dbSet
                .Include(t => t.Account)
                .Include(t => t.Bank)
                .ToListAsync();
        }

        public async Task<Tenant?> GetTenantWithSubscriptionByAccountIdAsync(Guid accountId)
        {
            return await _dbSet
                .Include(t => t.Account)
                .Include(t => t.Bank)
                .FirstOrDefaultAsync(t => t.AccountId == accountId);
        }

        public async Task<Tenant?> GetByIdWithAccountAsync(Guid tenantId)
        {
            return await _dbSet
                .Include(x => x.Account)
                .FirstOrDefaultAsync(x => x.Id == tenantId);
        }

        public async Task<bool> SuspendTenantAsync(Guid tenantId, bool isSuspended)
        {
            if (tenantId == Guid.Empty)
                return false;

            var tenant = await _dbSet.FindAsync(tenantId);

            if (tenant == null)
                return false;

            tenant.IsSuspended = isSuspended;
            tenant.SuspendedAt = isSuspended ? DateTime.UtcNow : null;

            var result = await _context.SaveChangesAsync();
            return result > 0;
        }
    }
}
