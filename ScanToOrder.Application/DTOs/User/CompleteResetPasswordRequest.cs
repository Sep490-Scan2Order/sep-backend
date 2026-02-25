namespace ScanToOrder.Application.DTOs.User
{
    public class CompleteResetPasswordRequest
    {
        public string Email { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
        public string ResetToken { get; set; } = null!;
    }
}
