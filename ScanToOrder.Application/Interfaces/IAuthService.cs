using ScanToOrder.Application.DTOs.Auth;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Interfaces
{
    public interface IAuthService
    {
        Task<string> SendOtpAsync(string phone);

        Task<AuthResponse> VerifyAndLoginAsync(LoginRequest request);

        Task<AuthResponse> RegisterAsync(RegisterRequest request);

        Task<AuthResponse> TenantLoginAsync(TenantLoginRequest request);
        Task<AuthResponse> StaffLoginAsync(StaffLoginRequest request);

        Task<string> CompleteResetPasswordAsync(string email, string resetToken, string newPassword);
        Task<string> VerifyForgotPasswordOtpAsync(string email, string otpCode);
    }
}
