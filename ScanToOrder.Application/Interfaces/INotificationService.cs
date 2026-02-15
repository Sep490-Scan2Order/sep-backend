using ScanToOrder.Application.DTOs.Notification;

namespace ScanToOrder.Application.Interfaces
{
    public interface INotificationService
    {
        Task<CreateNotificationDtoResponse> CreateNotificationAsync(CreateNotificationDtoRequest request);
    }
}
