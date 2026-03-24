using ScanToOrder.Domain.Entities.Shifts;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IShiftReportRepository : IGenericRepository<ShiftReport>
    {
        Task<List<(ShiftReport Report, decimal OpeningCashAmount)>> GetReportsByRestaurantAsync(
            int restaurantId, DateTime? from, DateTime? to);

        Task<List<(ShiftReport Report, decimal OpeningCashAmount)>> GetReportsByStaffAsync(Guid staffId);

        Task<(decimal TotalCash, decimal TotalTransfer, decimal TotalRefund)> GetPaymentMetricsAsync(int restaurantId, DateTime startDate, DateTime endDate);
    }
}
