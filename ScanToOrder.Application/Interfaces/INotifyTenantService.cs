using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.DTOs.NotifyTenant;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.Interfaces
{
    public interface INotifyTenantService
    {
        Task<List<CreateNotifyTenantDtoResponse>> CreateNotifyTenantAsync(CreateNotifyTenantDtoRequest request);
        Task<IEnumerable<NotifyTenant>> GetNotifyTenantsAsync();
        Task<int> CountTotalNotifyByTenantId(Guid tenantId, NotifyTenantStatus? status = null);
        Task<string> UpdateStatusToReadAsync(Guid tenantId, UpdateNotifyTenantStatusRequestDto updateNotifyTenantStatusRequestDto);
        Task<(List<NotifyDetailDtoResponse> Items, int TotalCount)> GetNotifyDetailsByTenantIdSortBySentAtAsync(int pageIndex, int pageSize, Guid tenantId);
    }
}
