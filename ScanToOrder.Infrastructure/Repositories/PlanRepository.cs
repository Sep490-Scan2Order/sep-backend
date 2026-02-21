using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class PlanRepository : GenericRepository<Plan>, IPlanRepository
    {
        private readonly AppDbContext _context;
        public PlanRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
