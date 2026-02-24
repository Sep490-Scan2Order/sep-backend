using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScanToOrder.Application.DTOs.Auth;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IMemoryCache _cache;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;
        private readonly ISmsSender _smsSender;

        public AuthService(
            IMemoryCache cache,
            IUnitOfWork unitOfWork,
            IJwtService jwtService,
            ISmsSender smsSender)
        {
            _cache = cache;
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _smsSender = smsSender;
        }

        public async Task<string> SendOtpAsync(string phone)
        {
            string otpCode = new Random().Next(100000, 999999).ToString();

            _cache.Set("OTP_" + phone, otpCode, TimeSpan.FromMinutes(3));

            //await _smsSender.SendAsync(phone, otpCode);

            return otpCode;
        }

        public async Task<AuthResponse> VerifyAndLoginAsync(LoginRequest request)
        {
            var user = await _unitOfWork.AuthenticationUsers.GetByPhoneAsync(request.Phone);

            if (user == null)
            {
                throw new DomainException("Tài khoản chưa được đăng ký.");
            }

            if (user.IsActive == false)
            {
                throw new DomainException("Tài khoản đã bị khóa. Vui lòng liên hệ hỗ trợ để biết thêm chi tiết.");
            }

            if (string.IsNullOrEmpty(user.Password))
            {
                throw new DomainException("Tài khoản chưa đặt mật khẩu. Vui lòng đăng ký lại với mật khẩu.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new DomainException("Số điện thoại hoặc mật khẩu không đúng.");
            }

            return new AuthResponse
            {
                AccessToken = _jwtService.GenerateAccessToken(user),
                RefreshToken = _jwtService.GenerateRefreshToken(user)
            };
        }
        
        public async Task<AuthResponse> TenantLoginAsync(TenantLoginRequest request)
        {
            var user = await _unitOfWork.AuthenticationUsers.GetByEmailAsync(request.Email);
            if (user == null || user.Role != Role.Tenant)
            {
                throw new DomainException("Tài khoản chưa được đăng ký.");
            }

            if (string.IsNullOrEmpty(user.Password))
            {
                throw new DomainException("Tài khoản chưa đặt mật khẩu. Vui lòng đăng ký lại với mật khẩu.");
            }
            
            if (user.IsActive == false)
            {
                throw new DomainException("Tài khoản đã bị khóa. Vui lòng liên hệ hỗ trợ để biết thêm chi tiết.");
            }

            // if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            // {
            //     throw new DomainException("Số điện thoại hoặc mật khẩu không đúng.");
            // }
            
            if (request.Password != user.Password)
            {
                throw new DomainException("Mật khẩu không đúng.");
            }
            return new AuthResponse
            {
                AccessToken = _jwtService.GenerateAccessToken(user, ExtractProfileId(user)),
                RefreshToken = _jwtService.GenerateRefreshToken(user)
            };
        }
        
        private Guid? ExtractProfileId(AuthenticationUser user)
        {
            return user.Role switch
            {
                Role.Tenant => user.Tenant?.Id,
                Role.Staff => user.Staff?.Id,
                Role.Customer => user.Customer?.Id,
                _ => null
            };
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            ValidateOtpOrThrow(request.Phone, request.Otp);

            var existingUser = await _unitOfWork.AuthenticationUsers.GetByPhoneAsync(request.Phone);
            if (existingUser != null)
            {
                throw new DomainException("Số điện thoại này đã được đăng ký.");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new AuthenticationUser
            {
                Id = Guid.NewGuid(),
                Phone = request.Phone,
                Password = passwordHash,
                Email = string.Empty,
                Role = Role.Customer,
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

            return new AuthResponse
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
                throw new DomainException("Mã OTP không chính xác hoặc đã hết hạn.");
            }

            _cache.Remove(cacheKey);
        }
    }
}
