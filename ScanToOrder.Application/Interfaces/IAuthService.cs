using ScanToOrder.Application.DTOs.Auth;
using ScanToOrder.Application.DTOs.User;

namespace ScanToOrder.Application.Interfaces
{
    public interface IAuthService
    {
        Task<string> SendOtpAsync(string phone);

        Task<AuthResponse<CustomerDto>> VerifyAndLoginAsync(LoginRequest request);

        Task<AuthResponse<CustomerDto>> RegisterAsync(RegisterRequest request);

        Task<AuthResponse<TenantDto>> TenantLoginAsync(TenantLoginRequest request);
        Task<AuthResponse<StaffDto>> StaffLoginAsync(StaffLoginRequest request);

        Task<string> CompleteResetPasswordAsync(string email, string resetToken, string newPassword);
        Task<string> VerifyForgotPasswordOtpAsync(string email, string otpCode);
        Task<AuthResponse<AdminDto>> AdministratorLoginAsync(AdminLoginRequest request);
    }
}
