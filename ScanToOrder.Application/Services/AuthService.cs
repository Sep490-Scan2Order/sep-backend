using AutoMapper;
using Microsoft.Extensions.Caching.Memory;
using ScanToOrder.Application.DTOs.Auth;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using StackExchange.Redis;

namespace ScanToOrder.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IMemoryCache _cache;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;
        private readonly ISmsSender _smsSender;
        private readonly IOtpRedisService _otpRedisService;
        private readonly IDatabase _redisDb;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IMapper _mapper;

        public AuthService(
            IMemoryCache cache,
            IUnitOfWork unitOfWork,
            IJwtService jwtService,
            ISmsSender smsSender,
            IOtpRedisService otpRedisService,
            IConnectionMultiplexer connectionMultiplexer, 
            IMapper mapper)
        {
            _cache = cache;
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _smsSender = smsSender;
            _otpRedisService = otpRedisService;
            _connectionMultiplexer = connectionMultiplexer;
            _mapper = mapper;
            _redisDb = connectionMultiplexer.GetDatabase();
        }

        public async Task<string> SendOtpAsync(string phone)
        {
            string otpCode = new Random().Next(100000, 999999).ToString();

            _cache.Set("OTP_" + phone, otpCode, TimeSpan.FromMinutes(3));

            //await _smsSender.SendAsync(phone, otpCode);

            return otpCode;
        }

        public async Task<AuthResponse<CustomerDto>> VerifyAndLoginAsync(LoginRequest request)
        {
            var user = await _unitOfWork.AuthenticationUsers.GetByPhoneAsync(request.Phone);

            if (user == null)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_NOT_FOUND);
            }

            if (user.IsActive == false)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_LOCKED);
            }

            if (string.IsNullOrEmpty(user.Password))
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_NO_PASSWORD);
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD_PHONE);
            }

            return new AuthResponse<CustomerDto>
            {
                AccessToken = _jwtService.GenerateAccessToken(user),
                RefreshToken = _jwtService.GenerateRefreshToken(user)
            };
        }

        public async Task<AuthResponse<TenantDto>> TenantLoginAsync(TenantLoginRequest request)
        {
            var user = await _unitOfWork.AuthenticationUsers.GetByEmailAsync(request.Email);
            if (user == null || user.Role != Domain.Enums.Role.Tenant)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_NOT_FOUND);
            }

            if (string.IsNullOrEmpty(user.Password))
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_NO_PASSWORD);
            }

            if (user.IsActive == false)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_LOCKED);
            }

             if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
             {
                 throw new DomainException(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD);
             }

            //if (!user.Password.Equals(request.Password))
            //{
            //    throw new DomainException(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD);
            //}

            var tenant = await _unitOfWork.Tenants.GetTenantWithSubscriptionByAccountIdAsync(user.Id);
            
            return new AuthResponse<TenantDto>
            {
                AccessToken = _jwtService.GenerateAccessToken(user, ExtractProfileId(user)),
                RefreshToken = _jwtService.GenerateRefreshToken(user),
                UserInfo = _mapper.Map<TenantDto>(tenant)
            };
        }       
        
        public async Task<AuthResponse<StaffDto>> StaffLoginAsync(StaffLoginRequest request)
        {
            var user = await _unitOfWork.AuthenticationUsers.GetByEmailAsync(request.Email);
            if (user == null || user.Role != Domain.Enums.Role.Staff)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_NOT_FOUND);
            }

            if (string.IsNullOrEmpty(user.Password))
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_NO_PASSWORD);
            }

            if (user.IsActive == false)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_LOCKED);
            }

            // if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            // {
            //     throw new DomainException(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD_PHONE);
            // }

            if (request.Password != user.Password)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD);
            }
            var tenant = await _unitOfWork.Tenants.GetByFieldsIncludeAsync(
                t => t.AccountId == user.Id,
                t => t.Account,
                t => t.Subscriptions.Select(s => s.Plan),
                t => t.Bank
            );
            return new AuthResponse<StaffDto>
            {
                AccessToken = _jwtService.GenerateAccessToken(user, ExtractProfileId(user)),
                RefreshToken = _jwtService.GenerateRefreshToken(user),
            };
        }

        public async Task<AuthResponse<AdminDto>> AdministratorLoginAsync(AdminLoginRequest request)
        {
            var user = await _unitOfWork.AuthenticationUsers.GetByEmailAsync(request.Email);
            if (user == null || user.Role != Domain.Enums.Role.Admin)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_NOT_FOUND);
            }

            if (string.IsNullOrEmpty(user.Password))
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_NO_PASSWORD);
            }

            if (user.IsActive == false)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_LOCKED);
            }

            // if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            // {
            //     throw new DomainException(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD_PHONE);
            // }

            if (request.Password != user.Password)
            {
                throw new DomainException(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD);
            }
          
            return new AuthResponse<AdminDto>
            {
                AccessToken = _jwtService.GenerateAccessToken(user, ExtractProfileId(user)),
                RefreshToken = _jwtService.GenerateRefreshToken(user),
                UserInfo = _mapper.Map<AdminDto>(user)
            };
        }

        private Guid? ExtractProfileId(AuthenticationUser user)
        {
            return user.Role switch
            {
                Domain.Enums.Role.Tenant => user.Tenant?.Id,
                Domain.Enums.Role.Staff => user.Staff?.Id,
                Domain.Enums.Role.Customer => user.Customer?.Id,
                _ => null
            };
        }

        public async Task<AuthResponse<CustomerDto>> RegisterAsync(RegisterRequest request)
        {
            ValidateOtpOrThrow(request.Phone, request.Otp);

            var existingUser = await _unitOfWork.AuthenticationUsers.GetByPhoneAsync(request.Phone);
            if (existingUser != null)
            {
                throw new DomainException(AuthMessage.AuthError.PHONE_REGISTERED);
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new AuthenticationUser
            {
                Id = Guid.NewGuid(),
                Phone = request.Phone,
                Password = passwordHash,
                Email = string.Empty,
                Role = Domain.Enums.Role.Customer,
                CreatedAt = DateTime.UtcNow,
                Verified = true
            };

            await _unitOfWork.AuthenticationUsers.AddAsync(user);

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                AccountId = user.Id,
                Name = string.Empty,
                Dob = null
            };

            await _unitOfWork.Customers.AddAsync(customer);

            await _unitOfWork.SaveAsync();

            return new AuthResponse<CustomerDto>
            {
                AccessToken = _jwtService.GenerateAccessToken(user),
                RefreshToken = _jwtService.GenerateRefreshToken(user)
            };
        }

        private void ValidateOtpOrThrow(string phone, string otp)
        {
            var cacheKey = "OTP_" + phone;
            if (!_cache.TryGetValue(cacheKey, out string? storedOtp) || storedOtp != otp)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }

            _cache.Remove(cacheKey);
        }

        public async Task<string> VerifyForgotPasswordOtpAsync(string email, string otpCode)
        {
            var savedOtp = await _otpRedisService.GetOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD);

            if (string.IsNullOrEmpty(savedOtp) || savedOtp != otpCode)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }

            string resetToken = Guid.NewGuid().ToString();
            var tokenKey = $"reset_token:{email}";

            await _redisDb.StringSetAsync(tokenKey, resetToken, TimeSpan.FromMinutes(10));

            await _otpRedisService.DeleteOtpTenantAsync(email, OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD);

            return resetToken;
        }

        public async Task<string> CompleteResetPasswordAsync(string email, string resetToken, string newPassword)
        {
            var tokenKey = $"reset_token:{email}";
            var savedToken = await _redisDb.StringGetAsync(tokenKey);

            if (string.IsNullOrEmpty(savedToken) || savedToken != resetToken)
            {
                throw new DomainException(OtpMessage.OtpError.OTP_INVALID);
            }

            // var tenant = await _unitOfWork.Tenants.FirstOrDefaultAsync(
            //     t => t.Account.Email == email,
            //     includeProperties: "Account"
            // );

            var tenant = await _unitOfWork.Tenants.GetByFieldsIncludeAsync(
                t => t.Account.Email == email,
                t => t.Account
            );

            if (tenant == null) throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);

            tenant.Account.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            _unitOfWork.Tenants.Update(tenant);
            await _unitOfWork.SaveAsync();

            await _redisDb.KeyDeleteAsync(tokenKey);

            return TenantMessage.TenantSuccess.TENANT_RESET_PASSWORD;
        }
    }
}
