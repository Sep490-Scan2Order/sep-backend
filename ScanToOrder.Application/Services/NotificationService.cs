using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Interfaces;
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
            var notification = new Domain.Entities.Notifications.Notification
            {
                NotifyTitle = request.NotifyTitle,
                NotifySub = request.NotifySub,
            };
            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.SaveAsync();
            return new CreateNotificationDtoResponse
            {
                Id = notification.NotificationId,
                NotifyTitle = notification.NotifyTitle,
                NotifySub = notification.NotifySub,
                SystemBlogUrl = notification.SystemBlogUrl,
                SentAt = notification.SentAt
            };
        }
    }
}
