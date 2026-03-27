using ScanToOrder.Domain.Entities.Shifts;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IShiftRepository : IGenericRepository<Shift>
    {
        Task<Shift?> GetCurrentShiftByStaffIdAsync(Guid staffId);
    }
}
