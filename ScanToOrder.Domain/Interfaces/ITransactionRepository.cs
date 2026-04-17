using ScanToOrder.Domain.Entities.Orders;

namespace ScanToOrder.Domain.Interfaces
{
    public interface ITransactionRepository : IGenericRepository<Transaction>
    {
        Task<Transaction?> GetPaymentTransactionByOrderIdAsync(Guid orderId);
        Task<Transaction?> GetTransactionByOrderIdAsync(Guid orderId);
        Task<List<Transaction>> GetSuccessfulTransactionsByShiftIdAsync(int shiftId);
    }
}

