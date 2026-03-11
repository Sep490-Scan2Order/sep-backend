using ScanToOrder.Domain.Entities.Orders;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IOrderRepository : IGenericRepository<Order>
    {
        Task<int> GetNextDailyOrderCodeAsync(int restaurantId, DateTime startUtc, DateTime endUtc, int dateInt);
    }
}
