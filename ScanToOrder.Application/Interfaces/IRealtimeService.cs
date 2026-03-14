namespace ScanToOrder.Application.Interfaces
{
    public interface IRealtimeService
    {
        Task SendNotificationToTenant(string tenantId, object message);
        Task NotifyCountChanged(string tenantId, int newCount);
        Task NotifyListChanged(string tenantId);
        Task NotifyTenantProfileChanged(string tenantId);
        Task NotifySubscriptionChanged(string tenantId);

        Task SendOrderToKitchen(string restaurantId, string orderId);
        Task NotifyOrderStatusChanged(string restaurantId, string orderId, int newStatus);
    }
}
