using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

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
        public async Task<ActionResult<ApiResponse<PagedResult<NotificationDtoResponse>>>> GetNotifications(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
        {
            var (items, totalCount) = await _notificationService.GetNotificationsAsync(pageIndex, pageSize);

            return Success(new PagedResult<NotificationDtoResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = pageIndex,
                PageSize = pageSize
            });
        }
    }
}
