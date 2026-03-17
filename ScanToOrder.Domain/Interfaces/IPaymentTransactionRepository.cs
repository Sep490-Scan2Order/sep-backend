using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IPaymentTransactionRepository : IGenericRepository<PaymentTransaction>
    {
        Task<List<(int Year, int Month, decimal Revenue)>> GetRevenueTrendRawAsync(DateTime startDate);
    }
}
