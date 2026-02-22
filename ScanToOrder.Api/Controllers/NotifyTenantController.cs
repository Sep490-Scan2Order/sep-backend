using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Notifications;

namespace ScanToOrder.Api.Controllers
{
    public class NotifyTenantController : BaseController
    {
        private readonly INotifyTenantService _notifyTenantService;
        public NotifyTenantController(INotifyTenantService notifyTenantService)
        {
            _notifyTenantService = notifyTenantService;
        }
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateNotifyTenantDtoResponse>>> CreateNotifyTenant(CreateNotifyTenantDtoRequest request)
        {
            var result = await _notifyTenantService.CreateNotifyTenantAsync(request);
            return Success(result);
        }
        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<NotifyTenant>>>> GetNotifyTenants()
        {
            var result = await _notifyTenantService.GetNotifyTenantsAsync();
            return Success(result);
        }
    }
}
