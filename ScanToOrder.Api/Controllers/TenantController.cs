using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;

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

        [HttpPut("{id}/updateStatus")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateTenantStatus(Guid id, [FromQuery] bool isActive)
        {
            var result = await _tenantService.UpdateTenantStatusAsync(id, isActive);

            if (!result)
                throw new DomainException("Tenant is already blocked");

            return Success(string.Empty);
        }

        [Authorize(Roles = "Tenant")]
        [HttpPut("tax-validation")]
        public async Task<ActionResult<ApiResponse<string>>> BlockTenant([FromQuery] string taxCode)
        {
            var result = await _tenantService.ValidationTaxCodeAsync(taxCode);
            if (!result)
                throw new DomainException("Mã số thuế không hợp lệ");

            return Success(string.Empty);
        }

        [Authorize(Roles = "Tenant")]
        [HttpPut]
        public async Task<ActionResult<ApiResponse<string>>> UpdateTenant([FromBody] UpdateTenantDtoRequest request)
        {
            var result = await _tenantService.UpdateTenantAsync(request);
            return Success(result);
        }
    }
}