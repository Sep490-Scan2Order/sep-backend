namespace ScanToOrder.Application.Interfaces;

public interface ICronJobService
{
    Task CancelExpiredUnpaidOrdersAsync(CancellationToken cancellationToken = default);
    Task SyncBranchDishSellingStatusAsync(CancellationToken cancellationToken = default);
    Task SyncBranchDishPriceAsync(CancellationToken cancellationToken = default);
    Task UpdateRestaurantOpeningStatusAsync(CancellationToken cancellationToken = default);
    Task ProcessSubscriptionExpirationsAsync(CancellationToken cancellationToken = default);
    Task CalculateWeeklyCommissionFeeAsync(CancellationToken cancellationToken = default);
    Task MonitorAndSuspendOverdueDebtsAsync(CancellationToken cancellationToken = default);
    Task WarnUnpaidShiftsAsync(CancellationToken cancellationToken = default);
    Task CalculateBestSellersAndAIEligibilityAsync(CancellationToken cancellationToken = default);
}