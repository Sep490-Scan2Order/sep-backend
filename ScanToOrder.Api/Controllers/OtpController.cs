using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Otp;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Api.Controllers
{
    public class OtpController : BaseController
    {
        private readonly IOtpService _otpService;
        public OtpController(IOtpService otpService)
        {
            _otpService = otpService;
        }
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateOtp([FromBody] string email)
        {
            var otpCode = await _otpService.GenerateOtpAsync(email);
            return Ok(otpCode);
        }
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateOtp([FromBody] OtpValidationRequest request)
        {
            bool isValid = await _otpService.ValidateOtpAsync(request.Email, request.Otp);
            return Ok(isValid);
        }
    }
}
