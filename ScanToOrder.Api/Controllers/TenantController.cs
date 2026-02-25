using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Api.Controllers
{
    public class TenantController : BaseController
    {
        private readonly ITenantService _tenantService;
        private readonly IBankLookupService _lookupService;

        public TenantController(ITenantService tenantService, IBankLookupService lookupService)
        {
            _tenantService = tenantService;
            _lookupService = lookupService;
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
        
        // Validation
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
        [HttpPost("bank-lookup")]
        public async Task<ActionResult<ApiResponse<object?>>> LookupBank([FromBody] BankLookRequest request)
        {
            return Success<object?>(await _lookupService.LookupAccountAsync(request));
        }
        
        [Authorize(Roles = "Tenant")]
        [HttpPut("update-bank-info")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateBankInfo([FromQuery] Guid bankId, [FromQuery] string accountNumber)
        {
            var result=  await _tenantService.UpdateBankInfoAsync(bankId, accountNumber);
            return Success(result,"Cập nhật thông tin ngân hàng thành công, vui lòng chuyen khoản 10.000 VND để xác thực tài khoản");
        }
        //

        [Authorize(Roles = "Tenant")]
        [HttpPut]
        public async Task<ActionResult<ApiResponse<string>>> UpdateTenant([FromBody] UpdateTenantDtoRequest request)
        {
            var result = await _tenantService.UpdateTenantAsync(request);
            return Success(result);
        }
    }
}