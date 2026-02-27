using AutoMapper;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Utils;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITaxService _taxService;
        private readonly IBankLookupService _bankLookupService;
        private readonly IOtpRedisService _otpRedisService;
        private readonly ITenantWalletService _tenantWalletService;
        private readonly IAuthenticatedUserService _authenticatedUserService;
        private readonly ITransactionRedisService _transactionRedisService;

        public TenantService(
            IUnitOfWork unitOfWork, 
            IMapper mapper,
            ITaxService taxService, 
            IOtpRedisService otpRedisService,
            ITenantWalletService tenantWalletService, 
            IAuthenticatedUserService authenticatedUserService, 
            IBankLookupService bankLookupService, 
            ITransactionRedisService transactionRedisService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _taxService = taxService;
            _otpRedisService = otpRedisService;
            _tenantWalletService = tenantWalletService;
            _authenticatedUserService = authenticatedUserService;
            _bankLookupService = bankLookupService;
            _transactionRedisService = transactionRedisService;
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
            if (tenant.IsVerifyTax)
                throw new DomainException("Không thể cập nhật mã số thuế khi đã xác thực. Vui lòng liên hệ hỗ trợ để được trợ giúp.");
            
            var taxCodeExists = await _unitOfWork.Tenants.ExistsAsync(t => t.TaxNumber != null && t.TaxNumber.Equals(taxCode)); 
            if (taxCodeExists)
                throw new DomainException(TenantMessage.TenantError.TAX_CODE_ALREADY_EXISTS);
            
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
        
        public async Task<string> UpdateBankInfoAsync(Guid bankId, string accountNumber)
        {
            var tenantId = _authenticatedUserService.ProfileId!.Value;
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (tenant == null)
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            if (tenant.IsVerifyBank)
            {
                throw new DomainException("Không thể cập nhật thông tin ngân hàng khi đã xác thực. Vui lòng liên hệ hỗ trợ để được trợ giúp.");
            }

            var bankExists = await _unitOfWork.Banks.GetByFieldsIncludeAsync(b => b.Id == bankId);
            if (bankExists == null)
                throw new DomainException(BankMessage.BankError.BANK_NOT_FOUND);

            var result = await _bankLookupService.LookupAccountAsync(new BankLookRequest()
            {
                Bank = bankExists.Code,
                Account = accountNumber
            });
            if (!result.Success)
            {
                throw new DomainException("Thông tin tài khoản ngân hàng không hợp lệ");
            }
            tenant.BankId = bankId;
            tenant.CardNumber = accountNumber;
            tenant.IsVerifyBank = false;
            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveAsync();
            var qrResult = BankQrLinkUtils.GenerateSePayQrUrl(accountNumber, bankExists.Code, 10000, PaymentIntent.TenantVerification);

            string urlToDisplay = qrResult.QrUrl;
            string codeToSave = qrResult.PaymentCode;
            await _transactionRedisService.SaveTransactionCodeAsync(codeToSave, tenantId);
            return urlToDisplay;
        }
        
        public async Task<bool> VerifyBankAccountAsync(string paymentCode)
        {
            var tenantId = await _transactionRedisService.GetTenantIdByTransactionCodeAsync(paymentCode);
            if (tenantId != null)
            {
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(Guid.Parse(tenantId));
                if (tenant == null)
                    throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);

                if (string.IsNullOrEmpty(tenant.CardNumber) || tenant.BankId == null)
                    throw new DomainException("Thông tin ngân hàng chưa được cập nhật");

                tenant.IsVerifyBank = true;
                _unitOfWork.Tenants.Update(tenant);
                await _transactionRedisService.DeleteTransactionCodeAsync(paymentCode);
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
        public async Task<bool> UpdateTenantStatusAsync(Guid tenantId, bool isActive)
        {
            var tenant = await _unitOfWork.Tenants.GetByFieldsIncludeAsync(x => x.Id == tenantId, x => x.Account);
    
            if (tenant == null)
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);

            if (tenant.Account.IsActive == isActive)
            {
                throw new DomainException(isActive ? TenantMessage.TenantError.TENANT_ALREADY_ACTIVE : 
                                                     TenantMessage.TenantError.TENANT_ALREADY_BLOCKED);
            }

            tenant.Account.IsActive = isActive;

            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveAsync();

            return true;
        }

        public async Task<string> UpdateTenantAsync(UpdateTenantDtoRequest updateTenantDtoRequest)
        {
            var tenantId = _authenticatedUserService.ProfileId!.Value;
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (tenant == null)
                throw new NotFoundException("Tenant", tenantId);

            if (!string.IsNullOrEmpty(updateTenantDtoRequest.TaxNumber) && tenant.TaxNumber != updateTenantDtoRequest.TaxNumber)
            {
                var taxCodeExists = await _unitOfWork.Tenants.ExistsAsync(t => t.TaxNumber == updateTenantDtoRequest.TaxNumber);
                if (taxCodeExists)
                    throw new DomainException(TenantMessage.TenantError.TAX_CODE_ALREADY_EXISTS);

                var taxValidationResult = await _taxService.GetTaxCodeDetailsAsync(updateTenantDtoRequest.TaxNumber);
                if (!taxValidationResult.IsValid)
                    throw new DomainException(TenantMessage.TenantError.TAX_CODE_INVALID);

                tenant.Name = taxValidationResult.Representative;
            }

            if (updateTenantDtoRequest.BankId != Guid.Empty && tenant.BankId != updateTenantDtoRequest.BankId)
            {
                var bankExists = await _unitOfWork.Banks.ExistsAsync(b => b.Id == updateTenantDtoRequest.BankId);
                if (!bankExists)
                    throw new DomainException(BankMessage.BankError.BANK_NOT_FOUND);
            }

            _mapper.Map(updateTenantDtoRequest, tenant);

            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveAsync();

            return TenantMessage.TenantSuccess.TENANT_UPDATED;
        }
    }
}