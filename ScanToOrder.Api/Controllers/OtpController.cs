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
        public async Task<ActionResult<ApiResponse<string>>> VerifyTenantOtp([FromQuery] string email, [FromQuery] string inputOtp)
        {
            var savedOtp = await _otpRedisService.GetOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_REGISTER);

            if (string.IsNullOrEmpty(savedOtp))
            {
                throw new DomainException(OtpMessage.OtpError.OTP_UNKNOWN);
            }

            if (savedOtp != inputOtp)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }

            await _otpRedisService.DeleteOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_REGISTER);

            return Success(OtpMessage.OtpSuccess.OTP_VALIDATED);
        }

        [HttpPost("send-register")]
        public async Task<ActionResult<ApiResponse<string>>> SendRegisterTenantOtp([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException(EmailMessage.EmailError.EMAIL_NOT_NULL);
            }

            var result = await _otpRedisService.GenerateAndSaveOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_REGISTER);

            return Success(result);
        }

        [HttpPost("send-forgot-password")]
        public async Task<ActionResult<ApiResponse<string>>> SendForgotPasswordTenantOtp([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException(EmailMessage.EmailError.EMAIL_NOT_NULL);
            }
            var result = await _otpRedisService.GenerateAndSaveOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD);
            return Success(result);
        }

        [HttpGet("verify-forgot-password")]
        public async Task<ActionResult<ApiResponse<string>>> VerifyForgotPasswordTenantOtp([FromQuery] string email, [FromQuery] string inputOtp)
        {
            var savedOtp = await _otpRedisService.GetOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD);
            if (string.IsNullOrEmpty(savedOtp))
            {
                throw new DomainException(OtpMessage.OtpError.OTP_UNKNOWN);
            }
            if (savedOtp != inputOtp)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }
            await _otpRedisService.DeleteOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD);
            return Success(OtpMessage.OtpSuccess.OTP_VALIDATED);
        }

        [HttpPost("send-change-password")]
        public async Task<ActionResult<ApiResponse<string>>> SendChangePasswordTenantOtp([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException(EmailMessage.EmailError.EMAIL_NOT_NULL);
            }
            var result = await _otpRedisService.GenerateAndSaveOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_RESET_PASSWORD);
            return Success(result);
        }

        [HttpGet("verify-change-password")]
        public async Task<ActionResult<ApiResponse<string>>> VerifyChangePasswordTenantOtp([FromQuery] string email, [FromQuery] string inputOtp)
        {
            var savedOtp = await _otpRedisService.GetOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_RESET_PASSWORD);
            if (string.IsNullOrEmpty(savedOtp))
            {
                throw new DomainException(OtpMessage.OtpError.OTP_UNKNOWN);
            }
            if (savedOtp != inputOtp)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }
            await _otpRedisService.DeleteOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_RESET_PASSWORD);
            return Success(OtpMessage.OtpSuccess.OTP_VALIDATED);
        }
    }
}
