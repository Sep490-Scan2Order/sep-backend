using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Voucher;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ScanToOrder.Api.Controllers
{
    public class VoucherController : BaseController
    {
        private readonly IVoucherService _voucherService;
        public VoucherController(IVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<VoucherResponseDto>>> Create([FromBody] CreateVoucherDto request)
        {
            var result = await _voucherService.CreateAsync(request);
            return Success(result);
        }

        [Authorize(Roles = "Admin,Customer")]
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<VoucherResponseDto>>>> GetAll()
        {
            var result = await _voucherService.GetAllAsync();
            return Success(result);
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("redeem")]
        public async Task<ActionResult<ApiResponse<RedeemVoucherResponseDto>>> Redeem([FromBody] RedeemVoucherRequestDto request)
        {
            var sub = User.Identity?.Name
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var accountId))
            {
                throw new UnauthorizedAccessException("Token không hợp lệ.");
            }

            var result = await _voucherService.RedeemVoucherAsync(accountId, request);
            return Success(result);
        }
    }
}
