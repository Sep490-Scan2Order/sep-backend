using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class AuthenticationUserRepository : GenericRepository<AuthenticationUser>, IAuthenticationUserRepository
    {
        private readonly AppDbContext _context;

        public AuthenticationUserRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<AuthenticationUser?> GetByPhoneAsync(string phone)
        {
            return await _context.AuthenticationUsers
                .Include(u => u.Tenant)
                .Include(u => u.Staff)
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.Phone == phone);
        }

        public async Task<AuthenticationUser?> GetByEmailAsync(string email)
        {
            return await _context.AuthenticationUsers
                .Include(u => u.Tenant)
                .Include(u => u.Staff)
                .Include(u => u.Customer)
                .FirstOrDefaultAsync(u => u.Email == email);
        }
    }
}

