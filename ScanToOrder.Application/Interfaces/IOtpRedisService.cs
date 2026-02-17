using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Application.Interfaces
{
    public interface IOtpRedisService
    {
        Task SaveOtpAsync(string email, string otpCode, string purpose);
        Task<ApiResponse<string?>> GetOtpAsync(string email, string purpose);
        Task DeleteOtpAsync(string email, string purpose);
        Task<ApiResponse<string>> GenerateAndSaveOtpAsync(string email, string purpose);
    }
}
