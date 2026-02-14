using ScanToOrder.Application.DTOs.Auth;

namespace ScanToOrder.Application.Interfaces
{
    public interface IAuthService
    {
        Task<string> SendOtpAsync(string phone);

        Task<AuthResponse> VerifyAndLoginAsync(LoginRequest request);

        Task<AuthResponse> RegisterAsync(RegisterRequest request);
    }
}
