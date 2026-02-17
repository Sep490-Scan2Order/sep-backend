using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Notifications;

namespace ScanToOrder.Application.Interfaces
{
    public interface INotificationService
    {
        Task<ApiResponse<CreateNotificationDtoResponse>> CreateNotificationAsync(CreateNotificationDtoRequest request);
        Task<ApiResponse<IEnumerable<Notification>>> GetNotificationsAsync();
    }
}
