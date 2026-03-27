using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class ShiftRepository : GenericRepository<Shift>, IShiftRepository
    {
        public ShiftRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Shift?> GetCurrentShiftByStaffIdAsync(Guid staffId)
        {
            return await _context.Set<Shift>()
                .Where(x => x.StaffId == staffId
                            && x.EndDate == null)
                .OrderByDescending(x => x.StartDate)
                .FirstOrDefaultAsync();
        }
    }
}
