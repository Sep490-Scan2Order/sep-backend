using ScanToOrder.Domain.Entities.Notifications;

namespace ScanToOrder.Domain.Interfaces
{
    public interface INotifyTenantRepository : IGenericRepository<NotifyTenant>
    {
        Task<List<NotifyTenant>> GetDetailsByTenantIdAsync(Guid tenantId);
        Task<(List<NotifyTenant> Items, int TotalCount)> GetNotifyDetailsByTenantIdSortBySentAtAsync(int pageIndex, int pageSize, Guid tenantId);
    }
}
