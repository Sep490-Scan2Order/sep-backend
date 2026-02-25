using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Auth;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers;

public class AuthController : BaseController
{
    private readonly IAuthService _authService;
    private readonly IBankLookupService _lookupService;
    private readonly ITaxService _taxService;

    public AuthController(IAuthService authService, IBankLookupService lookupService, ITaxService taxService)
    {
        _authService = authService;
        _lookupService = lookupService;
        _taxService = taxService;
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

    [HttpPost("staff-login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] StaffLoginRequest request)
    {
        var result = await _authService.StaffLoginAsync(request);
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
    // Test
    [HttpPost("BankLookup")]
    public async Task<ActionResult<ApiResponse<object?>>> TestBank([FromBody] BankLookRequest request)
    {
        return Success<object?>(await _lookupService.LookupAccountAsync(request));
    }
    
    [HttpGet("Tax")]
    public async Task<ActionResult<ApiResponse<object?>>> TestTax([FromQuery] string taxCode)
    {
        return Success<object?>(await _taxService.GetTaxCodeDetailsAsync(taxCode));
    }

    [HttpPost("Complete-reset-password")]
    public async Task<ActionResult<ApiResponse<string>>> CompleteResetPassword([FromBody] CompleteResetPasswordRequest request)
    {
        var result = await _authService.CompleteResetPasswordAsync(request.Email, request.ResetToken, request.NewPassword);
        return Success(result);
    }

    [HttpPost("Verify-forgot-password-otp")]
    public async Task<ActionResult<ApiResponse<string>>> VerifyForgotPasswordOtp([FromBody] VerifyForgotPasswordOtpRequest request)
    {
        var result = await _authService.VerifyForgotPasswordOtpAsync(request.Email, request.OtpCode);
        return Success(result);
    }
}