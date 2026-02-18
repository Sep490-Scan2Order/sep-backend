using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Voucher;
using ScanToOrder.Application.Interfaces;
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
        public async Task<IActionResult> Create([FromBody] CreateVoucherDto request)
        {
            var result = await _voucherService.CreateAsync(request);
            return Success(result, "Tạo voucher thành công.");
        }

        [Authorize(Roles = "Admin,Customer")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _voucherService.GetAllAsync();
            return Success(result, "Lấy danh sách voucher thành công.");
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("redeem")]
        public async Task<IActionResult> Redeem([FromBody] RedeemVoucherRequestDto request)
        {
            var sub = User.Identity?.Name
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var accountId))
                return BadRequest(new { message = "Token không hợp lệ." });

            try
            {
                var result = await _voucherService.RedeemVoucherAsync(accountId, request);
                return Success(result, "Đổi điểm lấy voucher thành công.");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
