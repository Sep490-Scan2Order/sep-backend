using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class PaymentTransactionRepository : GenericRepository<PaymentTransaction>, IPaymentTransactionRepository
    {
        public PaymentTransactionRepository(AppDbContext context) : base(context)
        {

        }
        public async Task<List<(int Year, int Month, decimal Revenue)>> GetRevenueTrendRawAsync(DateTime startDate, PaymentTransactionType type)
        {
            var data = await _dbSet
                .Where(pt => pt.Status == PaymentTransactionStatus.Success
                          && pt.PaymentTransactionType == type
                          && pt.PaymentDate >= startDate)
                .GroupBy(pt => new { pt.PaymentDate.Year, pt.PaymentDate.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();

            return data
                .Select(x => (x.Year, x.Month, x.Revenue))
                .ToList();
        }

        public async Task<decimal> GetTotalPlatformRevenueAsync()
        {
            return await _dbSet
                .Where(pt => pt.Status == PaymentTransactionStatus.Success)
                .SumAsync(pt => pt.TotalAmount);
        }

        public async Task<List<PaymentTransaction>> GetSuccessfulSubscriptionTransactionsAsync(DateTime startDate)
        {
            return await _dbSet
                .Where(pt => pt.Status == PaymentTransactionStatus.Success
                             && pt.PaymentTransactionType == PaymentTransactionType.Subscription
                             && pt.PaymentDate >= startDate)
                .ToListAsync();
        }
    }
}
