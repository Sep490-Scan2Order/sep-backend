using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IOrderRepository : IGenericRepository<Order>
    {
        Task<int> GetNextDailyOrderCodeAsync(int restaurantId, DateTime startUtc, DateTime endUtc, int dateInt);
        Task<List<Order>> GetOrdersForKdsAsync(int restaurantId, List<OrderStatus> statuses);
    }
}
