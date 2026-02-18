using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Domain.Entities.Notifications;

namespace ScanToOrder.Application.Interfaces
{
    public interface INotifyTenantService
    {
        Task<CreateNotifyTenantDtoResponse> CreateNotifyTenantAsync(CreateNotifyTenantDtoRequest request);
        Task<IEnumerable<NotifyTenant>> GetNotifyTenantsAsync();
    }
}
