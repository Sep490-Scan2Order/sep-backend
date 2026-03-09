using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class StaffRepository : GenericRepository<Staff>, IStaffRepository
    {
        public StaffRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Staff?> GetStaffAccountIdAsync(Guid accountId)
        {
            return await _dbSet.Include(t => t.Account).FirstOrDefaultAsync(t => t.AccountId == accountId);
        }

        public async Task<(List<Staff> Data, int TotalCount)> GetStaffByRestaurantAsync(
     int restaurantId,
     int page,
     int pageSize)
        {
            var query = _context.Staffs
                .Include(x => x.Restaurant)
                .Include(x => x.Account)   
                .Where(x => x.RestaurantId == restaurantId);

            var totalCount = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, totalCount);
        }

        public async Task<List<Staff>> GetAvailableCashiersAsync()
        {
            return await _context.Staffs
                .Include(x => x.Account)
                .Include(x => x.Restaurant)
                .Where(x => x.Account.Role == Role.Cashier )
                .ToListAsync();
        }
    }
}

