using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class PointHistoryRepository : GenericRepository<PointHistory>, IPointHistoryRepository
    {
        private readonly AppDbContext _context;
        public PointHistoryRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
