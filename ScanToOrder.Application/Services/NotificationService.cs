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
            };
            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.SaveAsync();
            return new CreateNotificationDtoResponse
            {
                Id = notification.NotificationId,
                NotifyTitle = notification.NotifyTitle,
                NotifySub = notification.NotifySub,
                SentAt = notification.CreatedAt
            };
        }

        public async Task<IEnumerable<Notification>> GetNotificationsAsync()
        {
            var notifications = await _unitOfWork.Notifications.GetAllAsync();
            return notifications;
        }
    }
}
