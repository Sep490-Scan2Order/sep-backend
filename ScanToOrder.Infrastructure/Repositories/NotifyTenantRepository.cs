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
    }
}

