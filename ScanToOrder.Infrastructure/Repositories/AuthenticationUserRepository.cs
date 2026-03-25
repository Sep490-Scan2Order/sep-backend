using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class AuthenticationUserRepository : GenericRepository<AuthenticationUser>, IAuthenticationUserRepository
    {
        public AuthenticationUserRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<AuthenticationUser?> GetByPhoneAsync(string phone)
        {
            return await _dbSet
                .Include(u => u.Tenant)
                .Include(u => u.Staff)
                .FirstOrDefaultAsync(u => u.Phone == phone);
        }

        public async Task<AuthenticationUser?> GetByEmailAsync(string email)
        {
            return await _dbSet
                .Include(u => u.Tenant)
                .Include(u => u.Staff)
                .FirstOrDefaultAsync(u => u.Email == email);
        }
    }
}

