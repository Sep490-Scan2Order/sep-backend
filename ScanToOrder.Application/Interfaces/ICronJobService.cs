namespace ScanToOrder.Application.Interfaces;

public interface ICronJobService
{
    Task CancelExpiredUnpaidOrdersAsync(CancellationToken cancellationToken = default);
}