using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
    {
        public NotificationRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<(List<Notification> Items, int TotalCount)> GetNotificationSortBySentAtAsync(int pageIndex, int pageSize)
        {
            var actualPageIndex = pageIndex <= 0 ? 1 : pageIndex;
            var actualPageSize = pageSize <= 0 ? 20 : pageSize;
            var offset = (actualPageIndex - 1) * actualPageSize;

            var query = _dbSet
                .Where(r => r.NotifyTitle != null);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.SentAt)
                .Skip(offset)
                .Take(actualPageSize)
                .ToListAsync();
            return (items, totalCount);
        }
    }
}

