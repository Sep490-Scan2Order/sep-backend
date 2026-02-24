namespace ScanToOrder.Application.Interfaces
{
    public interface IOtpRedisService
    {
        Task SaveOtpTenantAsync(string email, string otpCode, string purpose);
        Task<string?> GetOtpTenantAsync(string email, string purpose);
        Task DeleteOtpTenantAsync(string email, string purpose);
        Task<string> GenerateAndSaveOtpTenantAsync(string email, string purpose);
    }
}
