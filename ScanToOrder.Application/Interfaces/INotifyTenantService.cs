using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Notifications;

namespace ScanToOrder.Application.Interfaces
{
    public interface INotifyTenantService
    {
        Task<ApiResponse<CreateNotifyTenantDtoResponse>> CreateNotifyTenantAsync(CreateNotifyTenantDtoRequest request);
        Task<ApiResponse<IEnumerable<NotifyTenant>>> GetNotifyTenantsAsync();
    }
}
