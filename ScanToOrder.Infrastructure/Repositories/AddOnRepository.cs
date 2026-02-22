using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class AddOnRepository : GenericRepository<AddOn>, IAddOnRepository
    {
        private readonly AppDbContext _context;
        public AddOnRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
