using Microsoft.AspNetCore.SignalR;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Hubs;
using System.Text.Json;

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

        public async Task NotifyTenantProfileChanged(string tenantId)
        {
            await _hubContext.Clients.Group(tenantId).SendAsync("ProfileChanged");
        }

        public async Task NotifySubscriptionChanged(string tenantId)
        {
            await _hubContext.Clients.Group(tenantId).SendAsync("SubscriptionChanged");
        }

        public async Task SendOrderToKitchen(string restaurantId, OrderRealtimeDto order)
        {
            Console.WriteLine($"Sent order {JsonSerializer.Serialize(order)} to restaurant {restaurantId}");
            await _hubContext.Clients
                .Group(restaurantId)
                .SendAsync("ReceiveOrder", order);
         
        }

        public async Task NotifyOrderCountChanged(string restaurantId, int newCount)
        {
            await _hubContext.Clients.Group(restaurantId).SendAsync("CountOrderChanged", newCount);
        }
        public async Task NotifyOrderStatusChanged(string restaurantId, string orderId, int newStatus)
        {
            Console.WriteLine($"Sent order status change for order {orderId} with new status {newStatus} to restaurant {restaurantId}");
            await _hubContext.Clients.Group(restaurantId).SendAsync("UpdateStatus", new { OrderId = orderId, Status = newStatus });
        }

        public async Task NotifyCustomerOrderStatusChanged(string orderId, int newStatus)
        {
            var group = $"order:{orderId}";
            Console.WriteLine($"Sent customer order status change | orderId={orderId} status={newStatus} group={group}");
            await _hubContext.Clients.Group(group).SendAsync("CustomerUpdateStatus", new { OrderId = orderId, Status = newStatus });
        }

        public async Task NotifyPaymentReceived(string restaurantId, int orderCode, decimal amount, string audioUrl)
        {
            Console.WriteLine($"NotifyPaymentReceived called | restaurantId={restaurantId}, orderCode={orderCode}, amount={amount}, audioUrl={audioUrl}");
            await _hubContext.Clients.Group(restaurantId).SendAsync("PaymentReceived", new { orderCode, amount, audioUrl });
        }

        public async Task NotifyShiftChanged(string staffId, object shift)
        {
            Console.WriteLine($"NotifyShiftChanged called | staffId={staffId}, shift={JsonSerializer.Serialize(shift)}");
            var group = $"staff:{staffId}"; await _hubContext.Clients.Group(group).SendAsync("ShiftChanged", shift);
        }
    }
}
