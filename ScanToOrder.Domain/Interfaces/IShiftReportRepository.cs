using ScanToOrder.Domain.Entities.Shifts;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IShiftReportRepository : IGenericRepository<ShiftReport>
    {
        Task<(List<(ShiftReport Report, decimal OpeningCashAmount, string CashierName)> Items, int TotalCount)> GetReportsByRestaurantAsync(
            int restaurantId, DateTime? from, DateTime? to, int pageIndex = 1, int pageSize = 10);

        Task<(List<(ShiftReport Report, decimal OpeningCashAmount, string CashierName)> Items, int TotalCount)> GetReportsByStaffAsync(Guid staffId, int pageIndex = 1, int pageSize = 10);

        Task<(decimal TotalCash, decimal TotalTransfer, decimal TotalRefund)> GetPaymentMetricsAsync(int restaurantId, DateTime startDate, DateTime endDate);
    }
}
