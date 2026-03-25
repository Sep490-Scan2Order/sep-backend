using AutoMapper;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.DTOs.Orders;
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
        private readonly IAuthenticatedUserService _authenticatedUserService;
        private readonly ITransactionRedisService _transactionRedisService;
        private readonly IRealtimeService _realtimeService;

        public TenantService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ITaxService taxService,
            IOtpRedisService otpRedisService,
            IAuthenticatedUserService authenticatedUserService,
            IBankLookupService bankLookupService,
            ITransactionRedisService transactionRedisService,
            IRealtimeService realtimeService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _taxService = taxService;
            _otpRedisService = otpRedisService;
            _authenticatedUserService = authenticatedUserService;
            _bankLookupService = bankLookupService;
            _transactionRedisService = transactionRedisService;
            _realtimeService = realtimeService;
        }

        public async Task<string> RegisterTenantAsync(RegisterTenantRequest request)
        {
            if (!ValidationUtils.IsValidPassword(request.Password))
            {
                throw new DomainException(StaffMessage.StaffError.INVALID_PASSWORD);
            }

            var savedOtp = await _otpRedisService.GetOtpTenantAsync(request.Email, OtpMessage.OtpKeyword.OTP_REGISTER);

            if (string.IsNullOrEmpty(savedOtp) || savedOtp != request.OtpCode)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }

            var existingUser = await _unitOfWork.AuthenticationUsers.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_ALREADY_EXISTS);
            }
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var userEntity = _mapper.Map<AuthenticationUser>(request);

            userEntity.Password = passwordHash;
            userEntity.Verified = true;
            userEntity.Avatar = "https://ysafyqmiutvhohvsthnt.supabase.co/storage/v1/object/public/logo/logo_default.png";

            var tenantEntity = _mapper.Map<Tenant>(request);
            tenantEntity.AccountId = userEntity.Id;

            await _unitOfWork.AuthenticationUsers.AddAsync(userEntity);
            await _unitOfWork.Tenants.AddAsync(tenantEntity);
            await _unitOfWork.SaveAsync();
            
            await _otpRedisService.DeleteOtpTenantAsync(request.Email, OtpMessage.OtpKeyword.OTP_REGISTER);

            return TenantMessage.TenantSuccess.TENANT_REGISTERED;
        }

        public async Task<bool> ValidationTaxCodeAsync(string taxCode)
        {
            var tenantId = _authenticatedUserService.ProfileId!.Value;
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);

            if (tenant == null)
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            if (tenant.IsVerifyTax)
                throw new DomainException("Không thể cập nhật mã số thuế khi đã xác thực. Vui lòng liên hệ hỗ trợ để được trợ giúp.");

            var taxCodeExists = await _unitOfWork.Tenants.ExistsAsync(t => t.TaxNumber != null && t.TaxNumber.Equals(taxCode) && t.Id != tenantId); 
            if (taxCodeExists && !taxCode.Equals(tenant.TaxNumber))
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
            var qrResult = BankQrLinkUtils.GenerateSePayQrUrl(accountNumber, bankExists.ShortName, 10000, PaymentIntent.TenantVerification);

            string urlToDisplay = qrResult.QrUrl;
            string codeToSave = qrResult.PaymentCode;
            await _transactionRedisService.SaveTransactionCodeAsync(codeToSave, tenantId);
            return urlToDisplay;
        }

        public async Task<bool> VerifyBankAccountAsync(string paymentCode, string gateway, string accountNumber)
        {
            var tenantId = await _transactionRedisService.GetTenantIdByTransactionCodeAsync(paymentCode);
            var bank = await _unitOfWork.Banks.FirstOrDefaultAsync(b => b.ShortName == gateway);
            var result = await _bankLookupService.LookupAccountAsync(new BankLookRequest()
            {
                Bank = bank.Code,
                Account = accountNumber
            });
            if (!result.Success)
            {
                throw new DomainException("Thông tin tài khoản ngân hàng không hợp lệ");
            }
            if (tenantId != null)
            {
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(Guid.Parse(tenantId));
                if (tenant == null)
                    throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);

                if (string.IsNullOrEmpty(tenant.CardNumber) || tenant.BankId == null)
                    throw new DomainException("Thông tin ngân hàng chưa được cập nhật");
                if (BankQrLinkUtils.RemoveVietnameseTones(tenant.Name).ToLower().Equals(result.Data.OwnerName.ToLower()))
                {
                    tenant.IsVerifyTax = true;
                }
                tenant.IsVerifyBank = true;
                _unitOfWork.Tenants.Update(tenant);
                await _transactionRedisService.DeleteTransactionCodeAsync(paymentCode);
                await _unitOfWork.SaveAsync();
                
                await _realtimeService.NotifyTenantProfileChanged(tenantId);
                
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

        public async Task<TenantDto> GetTenantByIdAsync(Guid tenantId)
        {
            var tenant = await _unitOfWork.Tenants
                .GetByFieldsIncludeAsync(t => t.Id == tenantId, t => t.Account, t => t.Bank!);

            if (tenant == null)
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);

            return _mapper.Map<TenantDto>(tenant);
        }

        public async Task<TotalRevenueByTenantDto> GetTotalRevenueByTenantAsync(
            Guid? tenantId,
            DateTime? startDate,
            DateTime? endDate,
            string? preset)
        {
            var filter = ResolveFilter(startDate, endDate, preset);

            var resolvedTenantId = tenantId.HasValue && tenantId.Value != Guid.Empty
                ? tenantId.Value
                : _authenticatedUserService.ProfileId ?? throw new DomainException("Không xác định được tenant hiện tại.");

            var tenant = await _unitOfWork.Tenants.GetByIdAsync(resolvedTenantId);
            if (tenant == null)
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);

            var restaurants = await _unitOfWork.Restaurants
                .GetRestaurantsWithSubscriptionsByTenantIdAsync(resolvedTenantId);

            var revenueByRestaurant = await _unitOfWork.Orders
                .GetRevenueByTenantAsync(resolvedTenantId, filter.StartDate, filter.EndDate);

            var revenueMap = revenueByRestaurant.ToDictionary(x => x.RestaurantId);

            var restaurantDtos = _mapper.Map<List<TenantRestaurantRevenueDto>>(restaurants);
            foreach (var restaurantDto in restaurantDtos)
            {
                if (revenueMap.TryGetValue(restaurantDto.RestaurantId, out var revenue))
                {
                    restaurantDto.TotalOrders = revenue.TotalOrders;
                    restaurantDto.GrossRevenue = revenue.GrossRevenue;
                    restaurantDto.NetRevenue = revenue.NetRevenue;
                    restaurantDto.TotalDiscount = revenue.TotalDiscount;
                    restaurantDto.AverageOrderValue = revenue.TotalOrders > 0
                        ? revenue.NetRevenue / revenue.TotalOrders
                        : 0;
                }
            }

            var totalOrders = restaurantDtos.Sum(x => x.TotalOrders);
            var grossRevenue = restaurantDtos.Sum(x => x.GrossRevenue);
            var netRevenue = restaurantDtos.Sum(x => x.NetRevenue);
            var totalDiscount = restaurantDtos.Sum(x => x.TotalDiscount);

            return new TotalRevenueByTenantDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name ?? string.Empty,
                IsAllTime = filter.IsAllTime,
                FilterPreset = filter.Preset,
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                TotalRestaurants = restaurantDtos.Count,
                TotalOrders = totalOrders,
                GrossRevenue = grossRevenue,
                NetRevenue = netRevenue,
                TotalDiscount = totalDiscount,
                AverageOrderValue = totalOrders > 0 ? netRevenue / totalOrders : 0,
                Restaurants = restaurantDtos
            };
        }

        private static (DateTime? StartDate, DateTime? EndDate, bool IsAllTime, string Preset) ResolveFilter(
            DateTime? startDate,
            DateTime? endDate,
            string? preset)
        {
            var normalizedPreset = string.IsNullOrWhiteSpace(preset)
                ? null
                : preset.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(normalizedPreset))
            {
                var now = DateTime.UtcNow;
                var todayStart = now.Date;

                return normalizedPreset switch
                {
                    "alltime" => (null, null, true, "allTime"),
                    "today" => (todayStart, todayStart.AddDays(1).AddTicks(-1), false, "today"),
                    "last7days" => (todayStart.AddDays(-6), todayStart.AddDays(1).AddTicks(-1), false, "last7days"),
                    "last30days" => (todayStart.AddDays(-29), todayStart.AddDays(1).AddTicks(-1), false, "last30days"),
                    "thismonth" =>
                        (
                            new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                            new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddTicks(-1),
                            false,
                            "thisMonth"
                        ),
                    "thisyear" =>
                        (
                            new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                            new DateTime(now.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1),
                            false,
                            "thisYear"
                        ),
                    _ => throw new DomainException("preset không hợp lệ. Hỗ trợ: allTime, today, last7days, last30days, thisMonth, thisYear.")
                };
            }

            if (startDate.HasValue ^ endDate.HasValue)
            {
                throw new DomainException("Khi lọc theo ngày, cần truyền đủ cả startDate và endDate.");
            }

            if (!startDate.HasValue && !endDate.HasValue)
            {
                return (null, null, true, "allTime");
            }

            if (endDate!.Value < startDate!.Value)
            {
                throw new DomainException("endDate phải lớn hơn hoặc bằng startDate.");
            }

            var rangeDays = (endDate.Value.Date - startDate.Value.Date).TotalDays;
            if (rangeDays > 366)
            {
                throw new DomainException("Khoảng thời gian tối đa là 366 ngày. Dùng preset=allTime để xem toàn bộ.");
            }

            return (startDate.Value, endDate.Value, false, "custom");
        }
    }
}