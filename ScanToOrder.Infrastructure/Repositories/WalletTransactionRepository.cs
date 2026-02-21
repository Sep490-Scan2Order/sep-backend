using ScanToOrder.Domain.Entities.Wallet;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class WalletTransactionRepository : GenericRepository<WalletTransaction>, IWalletTransactionRepository
    {
        private readonly AppDbContext _context;
        public WalletTransactionRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
