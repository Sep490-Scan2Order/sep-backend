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

            await _smsSender.SendAsync(phone, otpCode);

            return otpCode;
        }

        public async Task<AuthResponse> VerifyAndLoginAsync(LoginRequest request)
        {
            ValidateOtpOrThrow(request.Phone, request.Otp);

            var user = await _unitOfWork.AuthenticationUsers.GetByPhoneAsync(request.Phone);

            if (user == null)
            {
                throw new DomainException("Tài khoản chưa được đăng ký.");
            }

            return new AuthResponse
            {
                AccessToken = _jwtService.GenerateAccessToken(user),
                RefreshToken = _jwtService.GenerateRefreshToken(user)
            };
        }

        public async Task<AuthResponse> RegisterAsync(LoginRequest request)
        {

            ValidateOtpOrThrow(request.Phone, request.Otp);

            var existingUser = await _unitOfWork.AuthenticationUsers.GetByPhoneAsync(request.Phone);
            if (existingUser != null)
            {
                throw new DomainException("Số điện thoại này đã được đăng ký.");
            }

            var user = new AuthenticationUser
            {
                Id = Guid.NewGuid(),
                Phone = request.Phone,
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
