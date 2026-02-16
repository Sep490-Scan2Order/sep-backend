using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Template;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IOtpRepository _otpRepository;
        public OtpService(IUnitOfWork unitOfWork, IEmailService emailService, IOtpRepository otpRepository)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _otpRepository = otpRepository;
        }

        public async Task<string> GenerateOtpAsync(string email)
        {
            var otpCode = await _otpRepository.GenerateOtpAsync(email);

            string templateId = ResendTemplate.REGISTER_TEMPLATE_ID;

            var templateParams = new
            {
                OTP = int.Parse(otpCode)
            };

            await _emailService.SendEmailWithTemplateAsync(email, "Xác minh tài khoản Scan2Order", templateId, templateParams);

            return OtpMessage.OTP_GENERATED;
        }

        public async Task<bool> ValidateOtpAsync(string email, string otp)
        {
            bool isValid = await _otpRepository.ValidateOtpAsync(email, otp);
            return isValid;
        }
    }
}
