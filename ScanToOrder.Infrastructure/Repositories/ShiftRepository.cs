using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Enums;
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
                .Include(x => x.Staffs)
                .Where(x => x.StaffId == staffId
                            && x.Status == ShiftStatus.Open)
                .OrderByDescending(x => x.StartDate)
                .FirstOrDefaultAsync();
        }

        public async Task<Shift?> GetActiveCashierShiftAsync(int restaurantId)
        {
            return await _context.Set<Shift>()
                .FirstOrDefaultAsync(x => x.RestaurantId == restaurantId 
                                        && x.Type == ShiftType.Cashier 
                                        && x.Status == ShiftStatus.Open);
        }

        public async Task<bool> HasOpenSubShiftsAsync(int parentShiftId)
        {
            return await _context.Set<Shift>()
                .AnyAsync(x => x.ParentShiftId == parentShiftId
                            && x.Status == ShiftStatus.Open);
        }
        public async Task<IEnumerable<Shift>> GetOpenSubShiftsByParentIdAsync(int parentShiftId)
        {
            return await _context.Set<Shift>()
                .Include(x => x.Staffs)
                .Where(x => x.ParentShiftId == parentShiftId 
                            && x.Status == ShiftStatus.Open)
                .ToListAsync();
        }
    }
}
