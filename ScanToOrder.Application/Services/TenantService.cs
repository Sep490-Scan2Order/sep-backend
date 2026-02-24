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
        private readonly IAuthenticatedUserService _authenticatedUserService;

        public TenantService(IUnitOfWork unitOfWork, IMapper mapper, 
            ITaxService taxService, IOtpRedisService otpRedisService, 
            ITenantWalletService tenantWalletService, IAuthenticatedUserService authenticatedUserService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _taxService = taxService;
            _otpRedisService = otpRedisService;
            _tenantWalletService = tenantWalletService;
            _authenticatedUserService = authenticatedUserService;
        }

        public async Task<string> RegisterTenantAsync(RegisterTenantRequest request)
        {
            var savedOtp = await _otpRedisService.GetOtpTenantAsync(request.Email, OtpMessage.OtpKeyword.OTP_REGISTER);

            if (string.IsNullOrEmpty(savedOtp) || savedOtp != request.OtpCode)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }

            var existingUser = await _unitOfWork.AuthenticationUsers.GetByEmailAsync(request.Phone);
            if (existingUser != null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_ALREADY_EXISTS);
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

            await _otpRedisService.DeleteOtpTenantAsync(request.Email, OtpMessage.OtpKeyword.OTP_REGISTER);

            return TenantMessage.TenantSuccess.TENANT_REGISTERED;
        }
        
        public async Task<bool> ValidationTaxCodeAsync(string taxCode)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(_authenticatedUserService.ProfileId!.Value);
            if (tenant == null)
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            var result = await _taxService.GetTaxCodeDetailsAsync(taxCode);
            if (result.IsValid)
            {
                tenant.TaxNumber = taxCode;
                tenant.Name = result.Representative;
                _unitOfWork.Tenants.Update(tenant);
                await _unitOfWork.SaveAsync();
                return true;
            }
            return false;
        }
        
        public async Task<IEnumerable<TenantDto>> GetAllTenantsAsync()
        {
            var tenants = await _unitOfWork.Tenants.GetTenantsWithSubscriptionsAsync();

            return _mapper.Map<IEnumerable<TenantDto>>(tenants);
        }
        public async Task<bool> BlockTenantAsync(Guid tenantId)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdIncludeAsync(x => x.Id == tenantId, x => x.Account);

            if (tenant == null)
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);

            if (!tenant.Account.IsActive)
                return false;

            tenant.Account.IsActive = false;

            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveAsync();

            return true;
        }
    }
}