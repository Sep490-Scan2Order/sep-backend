using ScanToOrder.Application.DTOs.Notification;

namespace ScanToOrder.Application.Interfaces
{
    public interface INotifyTenantService
    {
        Task<CreateNotifyTenantDtoResponse> CreateNotifyTenantAsync(CreateNotifyTenantDtoRequest request);
    }
}
