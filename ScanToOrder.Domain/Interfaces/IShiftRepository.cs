using ScanToOrder.Domain.Entities.Shifts;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IShiftRepository : IGenericRepository<Shift>
    {
        Task<Shift?> GetCurrentShiftByStaffIdAsync(Guid staffId);
        Task<Shift?> GetActiveCashierShiftAsync(int restaurantId);
        Task<bool> HasOpenSubShiftsAsync(int parentShiftId);
        Task<IEnumerable<Shift>> GetOpenSubShiftsByParentIdAsync(int parentShiftId);
    }
}
