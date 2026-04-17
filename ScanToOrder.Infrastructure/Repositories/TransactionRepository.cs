using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

using ScanToOrder.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class TransactionRepository : GenericRepository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Transaction?> GetPaymentTransactionByOrderIdAsync(Guid orderId)
        {
            return await _context.Transactions
                .FirstOrDefaultAsync(t => t.OrderId == orderId && t.TransactionType == TransactionType.Payment);
        }

        public async Task<Transaction?> GetTransactionByOrderIdAsync(Guid orderId)
        {
            return await _context.Transactions
                .FirstOrDefaultAsync(t => t.OrderId == orderId);
        }

        public async Task<List<Transaction>> GetSuccessfulTransactionsByShiftIdAsync(int shiftId)
        {
            return await _context.Transactions
                .Include(t => t.Order)
                .Where(t => t.ShiftId == shiftId && t.Status == OrderTransactionStatus.Success)
                .ToListAsync();
        }
    }
}

