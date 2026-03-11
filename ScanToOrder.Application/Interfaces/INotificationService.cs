using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Domain.Entities.Notifications;

namespace ScanToOrder.Application.Interfaces
{
    public interface INotificationService
    {
        Task<CreateNotificationDtoResponse> CreateNotificationAsync(CreateNotificationDtoRequest request);
        Task<(List<NotificationDtoResponse> Items, int TotalCount)> GetNotificationsAsync(int pageIndex, int pageSize);
    }
}
