namespace ScanToOrder.Application.Interfaces
{
    public interface IRealtimeService
    {
        Task SendNotificationToTenant(string tenantId, object message);
        Task NotifyCountChanged(string tenantId, int newCount);
        Task NotifyListChanged(string tenantId);

        Task SendOrderToKitchen(string restaurantId, object order);
    }
}
