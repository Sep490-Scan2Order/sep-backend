using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;

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
        public async Task<ApiResponse<string>> VerifyOtp([FromQuery] string email, [FromQuery] string inputOtp)
        {
            var savedOtpResponse = await _otpRedisService.GetOtpAsync(email, OtpMessage.OtpKeyword.OTP_REGISTER);

            if (!savedOtpResponse.IsSuccess || string.IsNullOrEmpty(savedOtpResponse.Data))
            {
                return new ApiResponse<string>
                {
                    IsSuccess = false,
                    Message = OtpMessage.OtpError.OTP_UNKNOWN
                };
            }

            if (savedOtpResponse.Data != inputOtp)
            {
                return new ApiResponse<string>
                {
                    IsSuccess = false,
                    Message = OtpMessage.OtpError.OTP_INVALID
                };
            }

            await _otpRedisService.DeleteOtpAsync(email, OtpMessage.OtpKeyword.OTP_REGISTER);

            return new ApiResponse<string>
            {
                IsSuccess = true,
                Message = OtpMessage.OtpSuccess.OTP_VALIDATED
            };
        }

        [HttpPost("send-register")]
        public async Task<ApiResponse<string>> SendOtp([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
                return new ApiResponse<string>
                {
                    IsSuccess = false,
                    Message = EmailMessage.EmailError.EMAIL_NOT_NULL
                };

            var result = await _otpRedisService.GenerateAndSaveOtpAsync(email, OtpMessage.OtpKeyword.OTP_REGISTER);

            return result;
        }
    }
}
