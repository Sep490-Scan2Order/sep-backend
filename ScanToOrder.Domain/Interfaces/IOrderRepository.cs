using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IOrderRepository : IGenericRepository<Order>
    {
        Task<int> GetNextDailyOrderCodeAsync(int restaurantId, DateTime startUtc, DateTime endUtc, int dateInt);
        Task<List<Order>> GetOrdersForKdsAsync(int restaurantId);

        Task<Order?> GetOrderWithDetailsForKdsAsync(Guid orderId);

        Task<List<Order>> GetCashOrdersPendingConfirmAsync(int restaurantId);
        Task<Order?> GetByOrderCodeAndRestaurantAsync(int orderCode, int restaurantId);
        Task<List<Order>> GetExpiredUnpaidOrdersAsync(int minuteThreshold);
        Task<List<(int RestaurantId, string RestaurantName, string? Image,
                   int TotalOrders, decimal TotalRevenue,
                   string? PlanName, SubscriptionStatus? Status)>>
            GetTopRestaurantsFullDataAsync(int top);
        Task<List<Order>> GetRecentByRestaurantAndPhoneAsync(int restaurantId, string phone, int limit);
    }
}
