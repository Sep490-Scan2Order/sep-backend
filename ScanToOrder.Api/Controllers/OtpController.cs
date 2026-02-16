using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;

namespace ScanToOrder.Api.Controllers
{
    public class OtpController : BaseController
    {
        private readonly IOtpRedisService _otpRedisService;
        public OtpController(IOtpRedisService otpRedisService)
        {
            _otpRedisService = otpRedisService;
        }

        [HttpGet("verify-register")]
        public async Task<IActionResult> VerifyOtp([FromQuery] string email, [FromQuery] string inputOtp)
        {
            var savedOtp = await _otpRedisService.GetOtpAsync(email, OtpMessage.OTP_REGISTER);

            if (string.IsNullOrEmpty(savedOtp))
            {
                return BadRequest(new { message = "Mã OTP đã hết hạn hoặc không tồn tại." });
            }

            if (savedOtp != inputOtp)
            {
                return BadRequest(new { message = "Mã OTP không chính xác." });
            }

            await _otpRedisService.DeleteOtpAsync(email, OtpMessage.OTP_REGISTER);

            return Ok(new { message = "Xác thực OTP thành công!" });
        }

        [HttpPost("send-register")]
        public async Task<IActionResult> SendOtp([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
                return BadRequest("Email không được để trống.");

            string resultMessage = await _otpRedisService.GenerateAndSaveOtpAsync(email, OtpMessage.OTP_REGISTER);

            return Ok(new
            {
                message = resultMessage,
                expires_in = "30 minutes"
            });
        }
    }
}
