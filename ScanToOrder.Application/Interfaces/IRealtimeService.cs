using ScanToOrder.Application.DTOs.Orders;

namespace ScanToOrder.Application.Interfaces
{
    public interface IRealtimeService
    {
        Task SendNotificationToTenant(string tenantId, object message);
        Task NotifyCountChanged(string tenantId, int newCount);
        Task NotifyListChanged(string tenantId);
        Task NotifyTenantProfileChanged(string tenantId);
        Task NotifySubscriptionChanged(string tenantId);

        Task SendOrderToKitchen(string restaurantId, OrderRealtimeDto order);
        Task NotifyOrderStatusChanged(string restaurantId, string orderId, int newStatus);
        Task NotifyCustomerOrderStatusChanged(string orderId, int newStatus);
        Task NotifyPaymentReceived(string restaurantId, int orderCode, decimal amount, string audioUrl);
        Task NotifyShiftChanged(string staffId, object shift);
    }
}
