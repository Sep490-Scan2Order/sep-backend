using Microsoft.Extensions.Logging;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Utils;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class CronJobService : ICronJobService
{
        private readonly ILogger<CronJobService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderService _orderService;
        private readonly IDishRedisService _dishRedisService;
        private readonly IRealtimeService _realtimeService;
        private readonly ISubscriptionService _subscriptionService;

        public CronJobService(ILogger<CronJobService> logger, IUnitOfWork unitOfWork, 
            IOrderService orderService, IDishRedisService dishRedisService,
            IRealtimeService realtimeService, ISubscriptionService subscriptionService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _orderService = orderService;
            _dishRedisService = dishRedisService;
            _realtimeService = realtimeService;
            _subscriptionService = subscriptionService;
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

        public async Task UpdateRestaurantOpeningStatusAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: UpdateRestaurantOpeningStatusAsync vào lúc {Time}", DateTimeOffset.Now);

            try
            {
                var vnNow = TimeUtils.GetVietnamTimeNow();
                var nowTime = TimeOnly.FromDateTime(vnNow);

                var restaurants = await _unitOfWork.Restaurants.FindAsync(r => r.IsActive == true);
                bool hasChanges = false;

                foreach (var r in restaurants)
                {
                    if (!r.OpenTime.HasValue || !r.CloseTime.HasValue) continue;

                    bool isWithinHours = false;
                    if (r.OpenTime.Value < r.CloseTime.Value)
                    {
                        isWithinHours = nowTime >= r.OpenTime.Value && nowTime <= r.CloseTime.Value;
                    }
                    else
                    {
                        isWithinHours = nowTime >= r.OpenTime.Value || nowTime <= r.CloseTime.Value;
                    }

                    if (isWithinHours)
                    {
                        if (r.IsOpened != true)
                        {
                            r.IsOpened = true;
                            hasChanges = true;
                            _logger.LogInformation("Nhà hàng {Name} ({Id}) tự động MỞ.", r.RestaurantName, r.Id);
                        }
                    }
                    else
                    {
                        if (r.IsOpened == true)
                        {
                            r.IsOpened = false;
                            
                            if (r.IsReceivingOrders == true)
                            {
                                r.IsReceivingOrders = false;
                                await _realtimeService.NotifyReceivingOrdersChanged(r.Id.ToString(), false);
                            }

                            hasChanges = true;
                            _logger.LogInformation("Nhà hàng {Name} ({Id}) tự động ĐÓNG (Hết giờ).", r.RestaurantName, r.Id);
                        }
                    }
                }

                if (hasChanges)
                {
                    await _unitOfWork.SaveAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy CronJob: UpdateRestaurantOpeningStatusAsync");
            }

            _logger.LogInformation("Đã hoàn thành CronJob: UpdateRestaurantOpeningStatusAsync");
        }

        public async Task ProcessSubscriptionExpirationsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: ProcessSubscriptionExpirationsAsync vào lúc {Time}", DateTimeOffset.Now);
            
            try
            {
                await _subscriptionService.ProcessSubscriptionExpirationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy CronJob: ProcessSubscriptionExpirationsAsync");
            }
            
            _logger.LogInformation("Đã hoàn thành CronJob: ProcessSubscriptionExpirationsAsync");
        }
}