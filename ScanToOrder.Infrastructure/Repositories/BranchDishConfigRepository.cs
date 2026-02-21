using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class BranchDishConfigRepository : GenericRepository<BranchDishConfig>, IBranchDishConfigRepository
    {
        private readonly AppDbContext _context;
        public BranchDishConfigRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
