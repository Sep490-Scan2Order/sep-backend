using Microsoft.Extensions.Logging;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class CronJobService : ICronJobService
{
    private readonly ILogger<CronJobService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public CronJobService(ILogger<CronJobService> logger, IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }
    
    public async Task DailyTurnOffPromotionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Bắt đầu chạy CronJob: DailyTurnOffPromotionsAsync vào lúc {Time}", DateTimeOffset.Now);
        var promotionsToTurnOff = await _unitOfWork.Promotions.GetAllAsync();
        foreach (var p in promotionsToTurnOff)
        {
            p.IsActive = false;
        }
        
        _unitOfWork.Promotions.UpdateRange(promotionsToTurnOff);
        await _unitOfWork.SaveAsync();
        
        await Task.Delay(1000, cancellationToken); 
        _logger.LogInformation("Đã hoàn thành CronJob: DailyTurnOffPromotionsAsync");
    }
    
    
}