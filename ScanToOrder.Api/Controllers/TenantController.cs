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
        public async Task<ActionResult<ApiResponse<string>>> Register([FromBody] RegisterTenantRequest request)
        {
            var result = await _tenantService.RegisterTenantAsync(request);
            return Success(result);
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<ApiResponse<IEnumerable<TenantDto>>>> GetAll()
        {
            var result = await _tenantService.GetAllTenantsAsync();
            return Success(result);
        }

        [HttpPut("{id}/block")]
        public async Task<IActionResult> BlockTenant(Guid id)
        {
            var result = await _tenantService.BlockTenantAsync(id);

            if (!result)
                return BadRequest("Tenant is already blocked");

            return Ok("Tenant blocked successfully");
        }
    }
}