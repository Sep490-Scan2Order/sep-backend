using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class NotifyTenantRepository : GenericRepository<NotifyTenant>, INotifyTenantRepository
    {
        public NotifyTenantRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<NotifyTenant>> GetDetailsByTenantIdAsync(Guid tenantId)
        {
            return await _dbSet
                .Include(nt => nt.Notification)
                .Where(nt => nt.TenantId == tenantId)
                .OrderByDescending(nt => nt.Notification.SentAt)
                .ToListAsync();
        }

        public async Task<(List<NotifyTenant> Items, int TotalCount)> GetNotifyDetailsByTenantIdSortBySentAtAsync(int pageIndex, int pageSize, Guid tenantId)
        {
            
            var actualPageIndex = pageIndex <= 0 ? 1 : pageIndex;
            var actualPageSize = pageSize <= 0 ? 20 : pageSize;
            var offset = (actualPageIndex - 1) * actualPageSize;

            var query = _dbSet
                .Where(nt => nt.TenantId == tenantId);

            var totalCount = await query.CountAsync();
            var items = await query
                .Include(nt => nt.Notification)
                .OrderByDescending(nt => nt.Notification.SentAt)
                .Skip(offset)
                .Take(actualPageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}

