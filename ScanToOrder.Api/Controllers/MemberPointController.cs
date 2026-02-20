using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.MemberPoint;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ScanToOrder.Application.Services;

namespace ScanToOrder.Api.Controllers
{
    public class MemberPointController : BaseController
    {
        private readonly IMemberPointService _memberPointService;
        private readonly IAuthenticatedUserService _authenticatedUserService;
        public MemberPointController(IMemberPointService memberPointService, IAuthenticatedUserService authenticatedUserService)
        {
            _memberPointService = memberPointService;
            _authenticatedUserService = authenticatedUserService;
        }

        [HttpPost("add-member-point")]
        public async Task<ActionResult<ApiResponse<AddMemberPointDtoResponse>>> AddMemberPoint([FromBody] AddMemberPointDtoRequest memberPointDto)
        {
            var result = await _memberPointService.AddMemberPointAsync(memberPointDto);
            return Success(result);
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("my-point")]
        public async Task<ActionResult<ApiResponse<int>>> GetMyPoint()
        {
            var sub = User.Identity?.Name 
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var accountId))
            {
                throw new UnauthorizedAccessException("Token không hợp lệ.");
            }

            var point = await _memberPointService.GetCurrentPointAsync(_authenticatedUserService.UserId.Value);
            return Success(point);
        }
    }
}
