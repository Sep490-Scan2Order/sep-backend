using ScanToOrder.Domain.Entities.User;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IStaffRepository : IGenericRepository<Staff>
    {
    
        Task<Staff?> GetStaffAccountIdAsync(Guid accountId);
        Task<(List<Staff> Data, int TotalCount)> GetStaffByRestaurantAsync(
       int restaurantId,
       int page,
       int pageSize);
        Task<List<Staff>> GetAvailableCashiersAsync();
    }
}
