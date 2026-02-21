using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Wallet;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class TenantWalletRepository : GenericRepository<TenantWallet>, ITenantWalletRepository
    {
        private readonly AppDbContext _context;
        public TenantWalletRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
        
        public async Task<TenantWallet?> GetByTenantIdAsync(Guid tenantId)
        {
            return await _context.TenantWallets.FirstOrDefaultAsync(tw => tw.TenantId == tenantId);
        }
    }
}
