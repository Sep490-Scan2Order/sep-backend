using Microsoft.Extensions.Logging;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class CronJobService : ICronJobService
{
        private readonly ILogger<CronJobService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderService _orderService;
        private readonly IDishRedisService _dishRedisService;

        public CronJobService(ILogger<CronJobService> logger, IUnitOfWork unitOfWork, IOrderService orderService, IDishRedisService dishRedisService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _orderService = orderService;
            _dishRedisService = dishRedisService;
        }
        
        public async Task CancelExpiredUnpaidOrdersAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: CancelExpiredUnpaidOrdersAsync vào lúc {Time}", DateTimeOffset.Now);
            
            try
            {
                await _orderService.CancelExpiredUnpaidOrdersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy CronJob: CancelExpiredUnpaidOrdersAsync");
            }
            
            _logger.LogInformation("Đã hoàn thành CronJob: CancelExpiredUnpaidOrdersAsync");
        }

        public async Task SyncBranchDishSellingStatusAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: SyncBranchDishSellingStatusAsync vào lúc {Time}", DateTimeOffset.Now);
            
            try
            {
                var restaurantIds = await _dishRedisService.GetAllRestaurantsWithUnsyncedSellingStatusesAsync();
                int totalUpdated = 0;

                foreach (var restaurantId in restaurantIds)
                {
                    var dishStatuses = await _dishRedisService.GetDishSellingStatusesAsync(restaurantId);
                    if (!dishStatuses.Any()) continue;

                    var dishIds = dishStatuses.Keys;
                    
                    var configsToUpdate = await _unitOfWork.BranchDishConfigs
                        .FindAsync(x => x.RestaurantId == restaurantId && dishIds.Contains(x.DishId));

                    var branchDishConfigs = configsToUpdate.ToList();
                    if (!branchDishConfigs.Any()) continue;

                    foreach (var config in branchDishConfigs)
                    {
                        if (dishStatuses.TryGetValue(config.DishId, out bool isSelling))
                        {
                            config.IsSelling = isSelling;
                        }
                    }

                    _unitOfWork.BranchDishConfigs.UpdateRange(branchDishConfigs);
                    
                    await _dishRedisService.ClearSyncedSellingStatusesAsync(restaurantId);
                    totalUpdated += branchDishConfigs.Count();
                }

                if (totalUpdated > 0)
                {
                    await _unitOfWork.SaveAsync();
                    _logger.LogInformation("Đã đồng bộ {Count} bản ghi BranchDishConfig IsSelling từ Redis sang Database.", totalUpdated);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy CronJob: SyncBranchDishSellingStatusAsync");
            }
            
            _logger.LogInformation("Đã hoàn thành CronJob: SyncBranchDishSellingStatusAsync");
        }

        public async Task SyncBranchDishPriceAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: SyncBranchDishPriceAsync vào lúc {Time}", DateTimeOffset.Now);
            
            try
            {
                var restaurantIds = await _dishRedisService.GetAllRestaurantsWithUnsyncedPricesAsync();
                int totalUpdated = 0;

                foreach (var restaurantId in restaurantIds)
                {
                    var dishPrices = await _dishRedisService.GetDishPricesAsync(restaurantId);
                    if (!dishPrices.Any()) continue;

                    var dishIds = dishPrices.Keys;
                    
                    var configsToUpdate = await _unitOfWork.BranchDishConfigs
                        .FindAsync(x => x.RestaurantId == restaurantId && dishIds.Contains(x.DishId));

                    var branchDishConfigs = configsToUpdate.ToList();
                    if (!branchDishConfigs.Any()) continue;

                    foreach (var config in branchDishConfigs)
                    {
                        if (dishPrices.TryGetValue(config.DishId, out decimal newPrice))
                        {
                            config.Price = newPrice;
                        }
                    }

                    _unitOfWork.BranchDishConfigs.UpdateRange(branchDishConfigs);
                    
                    await _dishRedisService.ClearSyncedPricesAsync(restaurantId);
                    totalUpdated += branchDishConfigs.Count();
                }

                if (totalUpdated > 0)
                {
                    await _unitOfWork.SaveAsync();
                    _logger.LogInformation("Đã đồng bộ {Count} bản ghi BranchDishConfig Price từ Redis sang Database.", totalUpdated);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy CronJob: SyncBranchDishPriceAsync");
            }
            
            _logger.LogInformation("Đã hoàn thành CronJob: SyncBranchDishPriceAsync");
        }
}