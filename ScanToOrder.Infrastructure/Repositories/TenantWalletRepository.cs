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
    }
}
