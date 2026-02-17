using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
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

        public async Task<ApiResponse<CreateNotificationDtoResponse>> CreateNotificationAsync(CreateNotificationDtoRequest request)
        {
            var notification = new Notification
            {
                NotifyTitle = request.NotifyTitle,
                NotifySub = request.NotifySub,
            };
            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.SaveAsync();
            return new ApiResponse<CreateNotificationDtoResponse>
            {
                IsSuccess = true,
                Data = new CreateNotificationDtoResponse
                {
                    Id = notification.NotificationId,
                    NotifyTitle = notification.NotifyTitle,
                    NotifySub = notification.NotifySub,
                    SentAt = notification.CreatedAt
                }
            };
        }

        public async Task<ApiResponse<IEnumerable<Notification>>> GetNotificationsAsync()
        {
            var notifications = await _unitOfWork.Notifications.GetAllAsync();
            return new ApiResponse<IEnumerable<Notification>>
            {
                IsSuccess = true,
                Data = notifications
            };
        }
    }
}
