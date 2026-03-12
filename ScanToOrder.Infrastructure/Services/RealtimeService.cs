using Microsoft.AspNetCore.SignalR;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Hubs;

namespace ScanToOrder.Infrastructure.Services
{
    public class RealtimeService : IRealtimeService
    {
        private readonly IHubContext<Scan2OrderRealtimeHub> _hubContext;

        public RealtimeService(IHubContext<Scan2OrderRealtimeHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendNotificationToTenant(string tenantId, object message)
        {
            await _hubContext.Clients.Group(tenantId).SendAsync("ReceiveNotification", message);
        }

        public async Task NotifyCountChanged(string tenantId, int newCount)
        {
            await _hubContext.Clients.Group(tenantId).SendAsync("CountChanged", newCount);
        }

        public async Task NotifyListChanged(string tenantId)
        {
            await _hubContext.Clients.Group(tenantId).SendAsync("ListChanged");
        }

        public async Task SendOrderToKitchen(string restaurantId, object order)
        {
            await _hubContext.Clients.Group(restaurantId).SendAsync("ReceiveOrder", order);
        }
    }
}
