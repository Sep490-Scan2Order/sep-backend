using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        public NotificationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateNotificationDtoResponse> CreateNotificationAsync(CreateNotificationDtoRequest request)
        {
            var notification = new Notification
            {
                NotifyTitle = request.NotifyTitle,
                NotifySub = request.NotifySub,
                SystemBlogUrl = request.SystemBlogUrl,
            };
            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.SaveAsync();
            return new CreateNotificationDtoResponse
            {
                Id = notification.NotificationId,
                NotifyTitle = notification.NotifyTitle,
                NotifySub = notification.NotifySub,
                SystemBlogUrl = notification.SystemBlogUrl,
                SentAt = notification.CreatedAt
            };
        }

        public async Task<(List<NotificationDtoResponse> Items, int TotalCount)> GetNotificationsAsync(int pageIndex, int pageSize)
        {
            var (items, totalCount) = await _unitOfWork.Notifications.GetNotificationSortBySentAtAsync(pageIndex, pageSize);

            var responseItems = items.Select(n => new NotificationDtoResponse
            {
                NotificationId = n.NotificationId,
                NotifyTitle = n.NotifyTitle,
                NotifySub = n.NotifySub,
                SentAt = n.SentAt
            }).ToList();

            return (responseItems, totalCount);
        }
    }
}
