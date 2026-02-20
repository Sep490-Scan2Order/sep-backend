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
            if (_authenticatedUserService.UserId != null)
            {
                var point = await _memberPointService.GetCurrentPointAsync(_authenticatedUserService.UserId.Value);
                return Success(point);
            }
            throw new UnauthorizedAccessException("Token không hợp lệ.");
        }
    }
}
