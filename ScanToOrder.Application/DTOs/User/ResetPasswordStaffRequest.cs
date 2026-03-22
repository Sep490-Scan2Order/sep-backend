namespace ScanToOrder.Application.DTOs.User
{
    public class ResetPasswordStaffRequest
    {
        public string Email { get; set; } = null!;
        public string OldPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }
}
