using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Interfaces;

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
        public async Task<IActionResult> CreateNotifyTenant(CreateNotifyTenantDtoRequest request)
        {
            var result = await _notifyTenantService.CreateNotifyTenantAsync(request);
            return Ok(result);
        }
    }
}
