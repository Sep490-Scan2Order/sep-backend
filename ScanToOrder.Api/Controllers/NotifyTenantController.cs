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
        public async Task<ApiResponse<CreateNotifyTenantDtoResponse>> CreateNotifyTenant(CreateNotifyTenantDtoRequest request)
        {
            var result = await _notifyTenantService.CreateNotifyTenantAsync(request);
            return new ApiResponse<CreateNotifyTenantDtoResponse>
            {
                IsSuccess = result.IsSuccess,
                Data = result.Data,
                Message = result.Message
            };
        }
        [HttpGet]
        public async Task<ApiResponse<IEnumerable<NotifyTenant>>> GetNotifyTenants()
        {
            var result = await _notifyTenantService.GetNotifyTenantsAsync();
            return new ApiResponse<IEnumerable<NotifyTenant>>
            {
                IsSuccess = result.IsSuccess,
                Data = result.Data,
                Message = result.Message
            };
        }
    }
}
