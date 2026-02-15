using ScanToOrder.Domain.Entities.Notifications.ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class NotifyTenantRepository : GenericRepository<NotifyTenant>, INotifyTenantRepository
    {
        private readonly AppDbContext _context;
        public NotifyTenantRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
