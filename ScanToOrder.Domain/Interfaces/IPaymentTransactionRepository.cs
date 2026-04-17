using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IPaymentTransactionRepository : IGenericRepository<PaymentTransaction>
    {
        Task<List<(int Year, int Month, decimal Revenue)>> GetRevenueTrendRawAsync(DateTime startDate, ScanToOrder.Domain.Enums.PaymentTransactionType type);
        Task<decimal> GetTotalPlatformRevenueAsync();
        Task<List<PaymentTransaction>> GetSuccessfulSubscriptionTransactionsAsync(DateTime startDate);
    }
}
