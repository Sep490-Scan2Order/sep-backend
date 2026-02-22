using ScanToOrder.Domain.Entities.Wallet;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IWalletTransactionRepository : IGenericRepository<WalletTransaction>
    {
        Task<WalletTransaction?> GetByOrderCode(long orderCode);
    }
}
