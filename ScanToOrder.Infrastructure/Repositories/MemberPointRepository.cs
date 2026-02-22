using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class MemberPointRepository : GenericRepository<MemberPoint>, IMemberPointRepository
    {
        private readonly AppDbContext _context;
        public MemberPointRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<MemberPoint?> GetByAccountIdAsync(Guid accountId)
        {
            return await _context.MemberPoints
                .Include(mp => mp.Customer)
                .Where(mp => mp.Customer.AccountId == accountId)
                .OrderByDescending(mp => mp.RedeemAt)
                .FirstOrDefaultAsync();
        }
    }
}
