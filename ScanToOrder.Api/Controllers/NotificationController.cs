using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Interfaces;

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
        public async Task<IActionResult> CreateNotification(CreateNotificationDtoRequest request)
        {
            var result = await _notificationService.CreateNotificationAsync(request);
            return Ok(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetNotificationsByTenantId()
        {
            var result = await _notificationService.GetNotificationsByTenantIdAsync();
            return Ok(result);
        }
    }
}
