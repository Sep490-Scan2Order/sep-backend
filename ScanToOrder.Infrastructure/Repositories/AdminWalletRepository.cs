using ScanToOrder.Domain.Entities.Wallet;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class AdminWalletRepository : GenericRepository<AdminWallet>, IAdminWalletRepository
    {
        private readonly AppDbContext _context;
        public AdminWalletRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
