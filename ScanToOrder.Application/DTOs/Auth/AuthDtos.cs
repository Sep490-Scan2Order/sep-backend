namespace ScanToOrder.Application.DTOs.Auth
{
    public class SendOtpRequest
    {
        public string Phone { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    
    public class TenantLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }  

    public class RegisterRequest
    {
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }

    public class AuthResponse<T>
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    
        public T UserInfo { get; set; } = default!; 
    }
}
