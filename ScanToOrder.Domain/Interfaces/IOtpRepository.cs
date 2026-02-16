using ScanToOrder.Domain.Entities.OTPs;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IOtpRepository : IGenericRepository<OTP>
    {
        Task<string> GenerateOtpAsync(string email);
        Task<bool> ValidateOtpAsync(string email, string otp);
    }
}
