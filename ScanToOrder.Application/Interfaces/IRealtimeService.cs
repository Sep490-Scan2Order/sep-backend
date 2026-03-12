namespace ScanToOrder.Application.Interfaces
{
    public interface IRealtimeService
    {
        Task SendNotificationToTenant(string tenantId, object message);
        Task NotifyCountChanged(string tenantId, int newCount);
        Task NotifyListChanged(string tenantId);

        Task SendOrderToKitchen(string restaurantId, object order);
        Task NotifyOrderCountChanged(string restaurantId, int newCount);
        Task NotifyOrderStatusChanged(string restaurantId, string orderId, int newStatus);
    }
}
