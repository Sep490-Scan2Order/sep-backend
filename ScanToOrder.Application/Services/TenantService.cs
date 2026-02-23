using AutoMapper;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITaxService _taxService;
        private readonly IOtpRedisService _otpRedisService; 
        private readonly ITenantWalletService _tenantWalletService;

        public TenantService(IUnitOfWork unitOfWork, IMapper mapper, ITaxService taxService, IOtpRedisService otpRedisService, ITenantWalletService tenantWalletService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _taxService = taxService;
            _otpRedisService = otpRedisService;
            _tenantWalletService = tenantWalletService;
        }

        public async Task<string> RegisterTenantAsync(RegisterTenantRequest request)
        {
            var savedOtp = await _otpRedisService.GetOtpAsync(request.Email, OtpMessage.OtpKeyword.OTP_REGISTER);

            if (string.IsNullOrEmpty(savedOtp) || savedOtp != request.OtpCode)
            {
                throw new DomainException("Mã OTP không chính xác hoặc đã hết hạn.");
            }

            var existingUser = await _unitOfWork.AuthenticationUsers.GetByEmailAsync(request.Phone);
            if (existingUser != null)
            {
                throw new DomainException("Tài khoản đã tồn tại");
            }

            var userEntity = _mapper.Map<AuthenticationUser>(request);
            userEntity.Password = request.Password;
            userEntity.Verified = true;

            var tenantEntity = _mapper.Map<Tenant>(request);
            tenantEntity.AccountId = userEntity.Id;

            await _unitOfWork.AuthenticationUsers.AddAsync(userEntity);
            await _unitOfWork.Tenants.AddAsync(tenantEntity);
            await _unitOfWork.SaveAsync();

            await _tenantWalletService.CreateWalletTenantAsync(tenantEntity.Id);

            await _otpRedisService.DeleteOtpAsync(request.Email, "Register");

            return "Đăng ký tài khoản thành công!";
        }
        
        public async Task<IEnumerable<TenantDto>> GetAllTenantsAsync()
        {
            var tenants = await _unitOfWork.Tenants.GetTenantsWithSubscriptionsAsync();

            return _mapper.Map<IEnumerable<TenantDto>>(tenants);
        }
        public async Task<bool> BlockTenantAsync(Guid tenantId)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);

            if (tenant == null)
                throw new DomainException("Tenant not found");

            if (!tenant.Status)
                return false;

            tenant.Status = false;

            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveAsync();

            return true;
        }
    }
}