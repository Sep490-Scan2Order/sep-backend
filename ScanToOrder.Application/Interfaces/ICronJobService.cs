namespace ScanToOrder.Application.Interfaces;

public interface ICronJobService
{
    Task DailyTurnOffPromotionsAsync(CancellationToken cancellationToken = default);
}