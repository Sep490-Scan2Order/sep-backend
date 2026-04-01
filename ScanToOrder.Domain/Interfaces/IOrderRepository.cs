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
        Task<List<Order>> GetCustomerActiveOrdersAsync(int restaurantId, string phone);
        Task<List<Order>> GetCustomerActiveOrdersAllRestaurantsAsync(string phone);

        Task<(int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount, int RegularCount, decimal RegularRevenue, int RefundCount, decimal RefundRevenue)> GetRevenueMetricsAsync(int restaurantId, DateTime startDate, DateTime endDate);
        Task<List<(int DishId, string DishName, int QuantitySold, decimal Revenue)>> GetTopSellingDishesAsync(int restaurantId, DateTime startDate, DateTime endDate, int top);

        Task<List<(Guid TenantId, string TenantName, int TotalRestaurants, int TotalOrders, decimal TotalRevenue)>>
            GetTopTenantsByRevenueAsync(int top);

        Task<List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>>
            GetRevenueByTenantAsync(Guid tenantId, DateTime? startDate, DateTime? endDate);

        Task<(List<Order> Items, int TotalCount)> GetTenantOrdersPagedAsync(
            int restaurantId,
            int pageIndex,
            int pageSize,
            string? keyword = null,
            OrderStatus? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null);
    }
}
