using Microsoft.AspNetCore.SignalR;

namespace ScanToOrder.Infrastructure.Hubs
{
    public class Scan2OrderRealtimeHub : Hub
    {
        public async Task JoinOrderGroup(string orderId)
        {
            if (!Guid.TryParse(orderId, out var parsed))
                throw new HubException("OrderId không hợp lệ.");

            await Groups.AddToGroupAsync(Context.ConnectionId, $"order:{parsed}");
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task JoinRestaurantGroup(string restaurantId)
        { 
            await Groups.AddToGroupAsync(Context.ConnectionId, restaurantId);
        }
    }
}
