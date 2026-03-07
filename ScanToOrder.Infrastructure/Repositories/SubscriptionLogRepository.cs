using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class SubscriptionLogRepository : GenericRepository<SubscriptionLog>, ISubscriptionLogRepository
    {
        public SubscriptionLogRepository(AppDbContext context) : base(context)
        {
        }
    }
}
