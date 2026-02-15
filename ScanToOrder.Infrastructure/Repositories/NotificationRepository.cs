using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
    {
        private readonly AppDbContext _context;
        public NotificationRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
