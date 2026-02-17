using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.MemberPoint;
using ScanToOrder.Application.Interfaces;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ScanToOrder.Api.Controllers
{
    public class MemberPointController : BaseController
    {
        private readonly IMemberPointService _memberPointService;
        public MemberPointController(IMemberPointService memberPointService)
        {
            _memberPointService = memberPointService;
        }

        [HttpPost("add-member-point")]
        public async Task<IActionResult> AddMemberPoint([FromBody] AddMemberPointDtoRequest memberPointDto)
        {
            try
            {
                var result = await _memberPointService.AddMemberPointAsync(memberPointDto);
                return Success(result, "Thêm điểm thành viên thành công.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("my-point")]
        public async Task<IActionResult> GetMyPoint()
        {
            var sub = User.Identity?.Name 
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var accountId))
            {
                return BadRequest(new { message = "Token không hợp lệ." });
            }

            try
            {
                var point = await _memberPointService.GetCurrentPointAsync(accountId);
                return Success(point, "Lấy điểm thành viên hiện tại thành công.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
