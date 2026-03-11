using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.DTOs.NotifyTenant;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Api.Controllers
{
    public class NotifyTenantController : BaseController
    {
        private readonly INotifyTenantService _notifyTenantService;
        private readonly IAuthenticatedUserService _authenticatedUserService;
        public NotifyTenantController(INotifyTenantService notifyTenantService, IAuthenticatedUserService authenticatedUserService)
        {
            _notifyTenantService = notifyTenantService;
            _authenticatedUserService = authenticatedUserService;
        }
        [HttpPost]
        public async Task<ActionResult<ApiResponse<List<CreateNotifyTenantDtoResponse>>>> CreateNotifyTenant(CreateNotifyTenantDtoRequest request)
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

        [HttpGet("count/{tenantId:Guid}")]
        public async Task<ActionResult<ApiResponse<int>>> CountTotalNotifyByTenantId(Guid tenantId, NotifyTenantStatus? notifyTenantStatus)
        {
            var result = await _notifyTenantService.CountTotalNotifyByTenantId(tenantId, notifyTenantStatus);
            return Success(result);
        }

        [HttpPut("update-read-by-tenant")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateStatusToRead([FromBody] UpdateNotifyTenantStatusRequestDto request)
        {
            if (!_authenticatedUserService.ProfileId.HasValue)
            {
                return BadRequest(new { message = AuthMessage.AuthError.USER_PROFILE_NOT_FOUND });
            }
            var result = await _notifyTenantService.UpdateStatusToReadAsync(_authenticatedUserService.ProfileId.Value, request);
            return Success(result);
        }

        [HttpGet("details")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<PagedResult<NotifyDetailDtoResponse>>>> GetNotifiTenantDetails(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
        {
            if (!_authenticatedUserService.ProfileId.HasValue)
            {
                return BadRequest(new { message = AuthMessage.AuthError.USER_PROFILE_NOT_FOUND });
            }

            var (items, totalCount) = await _notifyTenantService.GetNotifyDetailsByTenantIdSortBySentAtAsync(
                pageIndex, pageSize, _authenticatedUserService.ProfileId.Value);

            return Success(new PagedResult<NotifyDetailDtoResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = pageIndex,
                PageSize = pageSize
            });
        }
    }
}
