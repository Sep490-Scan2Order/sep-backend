using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Auth;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers;

public class AuthController : BaseController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("send-otp")]
    public async Task<ActionResult<ApiResponse<string>>> SendOtp([FromBody] SendOtpRequest request)
    {
        var otp = await _authService.SendOtpAsync(request.Phone);
        return Success(otp);
    }

    [HttpPost("login-phone")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.VerifyAndLoginAsync(request);
        return Success(result);
    }
    
    [HttpPost("tenant-login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] TenantLoginRequest request)
    {
        var result = await _authService.TenantLoginAsync(request);
        return Success(result);
    }

    [HttpPost("register-phone")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return Success(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public ActionResult<ApiResponse<object?>> Logout()
    {
        return Success<object?>(null);
    }
}