using ScanToOrder.Domain.Entities.User;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IStaffRepository : IGenericRepository<Staff>
    {
        Task<Staff?> GetStaffAccountIdAsync(Guid accountId);
    }
}
