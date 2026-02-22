using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;

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
        public async Task<ActionResult<ApiResponse<string>>> VerifyOtp([FromQuery] string email, [FromQuery] string inputOtp)
        {
            var savedOtp = await _otpRedisService.GetOtpAsync(email, OtpMessage.OtpKeyword.OTP_REGISTER);

            if (string.IsNullOrEmpty(savedOtp))
            {
                throw new DomainException(OtpMessage.OtpError.OTP_UNKNOWN);
            }

            if (savedOtp != inputOtp)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }

            await _otpRedisService.DeleteOtpAsync(email, OtpMessage.OtpKeyword.OTP_REGISTER);

            return Success(OtpMessage.OtpSuccess.OTP_VALIDATED);
        }

        [HttpPost("send-register")]
        public async Task<ActionResult<ApiResponse<string>>> SendRegisterOtp([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException(EmailMessage.EmailError.EMAIL_NOT_NULL);
            }

            var result = await _otpRedisService.GenerateAndSaveOtpAsync(email, OtpMessage.OtpKeyword.OTP_REGISTER);

            return Success(result);
        }

        [HttpPost("send-forgot-password")]
        public async Task<ActionResult<ApiResponse<string>>> SendForgotPasswordOtp([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException(EmailMessage.EmailError.EMAIL_NOT_NULL);
            }
            var result = await _otpRedisService.GenerateAndSaveOtpAsync(email, OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD);
            return Success(result);
        }

        [HttpGet("verify-forgot-password")]
        public async Task<ActionResult<ApiResponse<string>>> VerifyForgotPasswordOtp([FromQuery] string email, [FromQuery] string inputOtp)
        {
            var savedOtp = await _otpRedisService.GetOtpAsync(email, OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD);
            if (string.IsNullOrEmpty(savedOtp))
            {
                throw new DomainException(OtpMessage.OtpError.OTP_UNKNOWN);
            }
            if (savedOtp != inputOtp)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }
            await _otpRedisService.DeleteOtpAsync(email, OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD);
            return Success(OtpMessage.OtpSuccess.OTP_VALIDATED);
        }

        [HttpPost("send-change-password")]
        public async Task<ActionResult<ApiResponse<string>>> SendChangePasswordOtp([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException(EmailMessage.EmailError.EMAIL_NOT_NULL);
            }
            var result = await _otpRedisService.GenerateAndSaveOtpAsync(email, OtpMessage.OtpKeyword.OTP_RESET_PASSWORD);
            return Success(result);
        }

        [HttpGet("verify-change-password")]
        public async Task<ActionResult<ApiResponse<string>>> VerifyChangePasswordOtp([FromQuery] string email, [FromQuery] string inputOtp)
        {
            var savedOtp = await _otpRedisService.GetOtpAsync(email, OtpMessage.OtpKeyword.OTP_RESET_PASSWORD);
            if (string.IsNullOrEmpty(savedOtp))
            {
                throw new DomainException(OtpMessage.OtpError.OTP_UNKNOWN);
            }
            if (savedOtp != inputOtp)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }
            await _otpRedisService.DeleteOtpAsync(email, OtpMessage.OtpKeyword.OTP_RESET_PASSWORD);
            return Success(OtpMessage.OtpSuccess.OTP_VALIDATED);
        }
    }
}
