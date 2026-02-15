using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;

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
        public async Task<IActionResult> Register([FromBody] RegisterTenantRequest request)
        {
            try
            {
                var result = await _tenantService.RegisterTenantAsync(request);

                return Success(result, "Đăng ký thành công.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { IsSuccess = false, Message = ex.Message });
            }
        }
    }
}