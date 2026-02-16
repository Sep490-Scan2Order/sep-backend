using ScanToOrder.Domain.Entities.OTPs;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class OtpRepository : GenericRepository<OTP>, IOtpRepository
    {
        private readonly AppDbContext _context;
        public OtpRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<string> GenerateOtpAsync(string email)
        {
            var otp = new Random().Next(100000, 999999).ToString();
            var otpEntity = new OTP
            {
                Email = email,
                OtpCode = otp,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5),
            };
            await _context.OTPs.AddAsync(otpEntity);
            await _context.SaveChangesAsync();
            return otp;
        }

        public async Task<bool> ValidateOtpAsync(string email, string otp)
        {
            var otpEntity = _context.OTPs.FirstOrDefault(o => o.Email == email && o.OtpCode == otp);
            if (otpEntity == null || otpEntity.ExpiredAt < DateTime.UtcNow)
            {
                return false;
            }
            otpEntity.IsUsed = true;
            otpEntity.Purpose = "Register";
            _context.OTPs.Update(otpEntity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
