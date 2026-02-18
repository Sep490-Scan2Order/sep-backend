using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Notifications;

namespace ScanToOrder.Api.Controllers
{
    public class NotificationController : BaseController
    {
        private readonly INotificationService _notificationService;
        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateNotificationDtoResponse>>> CreateNotification(CreateNotificationDtoRequest request)
        {
            var result = await _notificationService.CreateNotificationAsync(request);
            return Success(result);
        }
        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<Notification>>>> GetNotifications()
        {
            var result = await _notificationService.GetNotificationsAsync();
            return Success(result);
        }
    }
}
