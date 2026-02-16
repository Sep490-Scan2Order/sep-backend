using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Domain.Entities.Notifications;

namespace ScanToOrder.Application.Interfaces
{
    public interface INotificationService
    {
        Task<CreateNotificationDtoResponse> CreateNotificationAsync(CreateNotificationDtoRequest request);
        Task<IEnumerable<Notification>> GetNotificationsByTenantIdAsync();
    }
}
