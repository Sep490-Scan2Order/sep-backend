using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class StaffRepository : GenericRepository<Staff>, IStaffRepository
    {
        private readonly AppDbContext _context;
        public StaffRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Staff?> GetStaffAccountIdAsync(Guid accountId)
        {
            return await _dbSet.Include(t => t.Account).FirstOrDefaultAsync(t => t.AccountId == accountId);
        }
    }
}
