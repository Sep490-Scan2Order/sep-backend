using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class MemberPointRepository : GenericRepository<MemberPoint>, IMemberPointRepository
    {
        private readonly AppDbContext _context;
        public MemberPointRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
