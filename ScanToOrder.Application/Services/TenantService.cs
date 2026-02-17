using AutoMapper;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITaxService _taxService;
        private readonly IOtpRedisService _otpRedisService; 

        public TenantService(IUnitOfWork unitOfWork, IMapper mapper, ITaxService taxService, IOtpRedisService otpRedisService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _taxService = taxService;
            _otpRedisService = otpRedisService;
        }

        public async Task<ApiResponse<TenantDto>> RegisterTenantAsync(RegisterTenantRequest request)
        {
            var otpResponse = await _otpRedisService.GetOtpAsync(request.Email, OtpMessage.OtpKeyword.OTP_REGISTER);

            var savedOtp = otpResponse.Data;

            if (string.IsNullOrEmpty(savedOtp) || savedOtp != request.OtpCode)
            {
                throw new Exception("Mã OTP không chính xác hoặc đã hết hạn.");
            }

            if (!string.IsNullOrEmpty(request.TaxNumber))
            {
                var isValid = await _taxService.IsTaxCodeValidAsync(request.TaxNumber);
                if (!isValid) throw new Exception("Mã số thuế không hợp lệ hoặc đã ngừng hoạt động.");
            }

            var userEntity = _mapper.Map<AuthenticationUser>(request);
            userEntity.Password = request.Password;
            userEntity.Verified = true;

            var tenantEntity = _mapper.Map<Tenant>(request);
            tenantEntity.AccountId = userEntity.Id;

            await _unitOfWork.AuthenticationUsers.AddAsync(userEntity);
            await _unitOfWork.Tenants.AddAsync(tenantEntity);
            await _unitOfWork.SaveAsync();

            await _otpRedisService.DeleteOtpAsync(request.Email, "Register");

            return new ApiResponse<TenantDto>
            {
                IsSuccess = true,
                Message = "Đăng ký thành công",
                Data = _mapper.Map<TenantDto>(tenantEntity)
            };
        }
    }
}