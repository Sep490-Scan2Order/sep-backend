using ScanToOrder.Domain.Entities.Notifications;

namespace ScanToOrder.Domain.Interfaces
{
    public interface INotificationRepository : IGenericRepository<Notification>
    {
        Task<(List<Notification> Items, int TotalCount)> GetNotificationSortBySentAtAsync(int pageIndex, int pageSize);
    }
}
