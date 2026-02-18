using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class TenantController : BaseController
    {
        private readonly ITenantService _tenantService;

        public TenantController(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<TenantDto>>> Register([FromBody] RegisterTenantRequest request)
        {
            var result = await _tenantService.RegisterTenantAsync(request);
            return Success(result);
        }
    }
}