namespace ScanToOrder.Application.DTOs.User
{
    public class RegisterTenantRequest
    {
        public required string Phone { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string OtpCode { get; set; }
    }
}
