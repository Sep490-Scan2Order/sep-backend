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
        public async Task<List<(int Year, int Month, decimal Revenue)>> GetRevenueTrendRawAsync(DateTime startDate)
        {
            var data = await _dbSet
                .Where(pt => pt.Status == PaymentTransactionStatus.Success
                          && pt.CreatedAt >= startDate)
                .GroupBy(pt => new { pt.CreatedAt.Year, pt.CreatedAt.Month })
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
    }
}
