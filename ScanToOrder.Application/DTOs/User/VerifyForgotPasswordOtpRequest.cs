namespace ScanToOrder.Application.DTOs.User
{
    public class VerifyForgotPasswordOtpRequest
    {
        public string OtpCode { get; set; }
        public string Email { get; set; }
    }
}
