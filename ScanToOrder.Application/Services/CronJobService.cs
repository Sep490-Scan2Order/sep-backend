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
        private readonly IEmailService _emailService;
        private readonly IAIUpsellRedisService _aiUpsellRedisService;

        public CronJobService(ILogger<CronJobService> logger, IUnitOfWork unitOfWork, 
            IOrderService orderService, IDishRedisService dishRedisService,
            IRealtimeService realtimeService, ISubscriptionService subscriptionService,
            IEmailService emailService, IAIUpsellRedisService aiUpsellRedisService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _orderService = orderService;
            _dishRedisService = dishRedisService;
            _realtimeService = realtimeService;
            _subscriptionService = subscriptionService;
            _emailService = emailService;
            _aiUpsellRedisService = aiUpsellRedisService;
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

                            // Bổ sung: Tự động cho phép nhận đơn khi tới giờ mở cửa
                            if (r.IsReceivingOrders != true)
                            {
                                r.IsReceivingOrders = true;
                                await _realtimeService.NotifyReceivingOrdersChanged(r.Id.ToString(), true);
                            }

                            hasChanges = true;
                            _logger.LogInformation("Nhà hàng {Name} ({Id}) tự động MỞ và cho phép nhận đơn.", r.RestaurantName, r.Id);
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

        public async Task CalculateWeeklyCommissionFeeAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: CalculateWeeklyCommissionFeeAsync vào lúc {Time}", DateTimeOffset.Now);

            await using var dbTxn = await _unitOfWork.BeginTransactionAsync();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var servedUnscannedOrders = await _unitOfWork.Orders.GetAllAsync(
                    o => o.Status == OrderStatus.Served && !o.IsScanned,
                    o => o.Restaurant,
                    o => o.Restaurant.Subscription,
                    o => o.Restaurant.Subscription.Plan);

                var configuration = (await _unitOfWork.Configurations.GetAllAsync()).FirstOrDefault();
                var commissionRatePercent = configuration?.CommissionRate is > 0
                    ? configuration.CommissionRate
                    : 3;
                var commissionRate = commissionRatePercent / 100m;

                if (!servedUnscannedOrders.Any())
                {
                    await dbTxn.CommitAsync();
                    _logger.LogInformation("Không có đơn Served chưa quét để tính phí hoa hồng tuần này.");
                    return;
                }

                // Split into 2 groups:
                // - exemptOrders: belonging to plan with IsCommissionExempt=true (e.g. Trial) → mark scanned only, no fee
                // - chargeableOrders: normal plans → calculate commission as usual
                var exemptOrders = servedUnscannedOrders
                    .Where(o => o.Restaurant?.Subscription != null
                                && o.Restaurant.Subscription.Status == SubscriptionStatus.Active
                                && o.Restaurant.Subscription.Plan?.IsCommissionExempt == true)
                    .ToList();

                var chargeableOrders = servedUnscannedOrders
                    .Except(exemptOrders)
                    .ToList();

                if (exemptOrders.Any())
                    _logger.LogInformation("{Count} đơn thuộc gói miễn hoa hồng — đánh IsScanned nhưng không tính phí.", exemptOrders.Count);

                var ordersByTenant = chargeableOrders
                    .GroupBy(o => o.Restaurant.TenantId)
                    .ToList();

                var tenantIds = ordersByTenant.Select(g => g.Key).Distinct().ToList();
                var tenantMap = (await _unitOfWork.Tenants.FindAsync(t => tenantIds.Contains(t.Id)))
                    .ToDictionary(t => t.Id);

                var updatedTenants = new List<Domain.Entities.User.Tenant>();
                foreach (var group in ordersByTenant)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!tenantMap.TryGetValue(group.Key, out var tenant))
                    {
                        _logger.LogWarning("Không tìm thấy Tenant {TenantId} khi tính phí hoa hồng.", group.Key);
                        continue;
                    }

                    var totalFee = group.Sum(x => x.FinalAmount) * commissionRate;
                    if (totalFee <= 0) continue;

                    tenant.TotalDebtAmount += totalFee;
                    if (tenant.DebtStartedAt == null)
                    {
                        tenant.DebtStartedAt = DateTime.UtcNow;
                    }

                    updatedTenants.Add(tenant);
                }

                // Mark ALL orders (exempt + chargeable) as scanned to prevent future re-processing
                foreach (var order in servedUnscannedOrders)
                {
                    order.IsScanned = true;
                    order.Restaurant = null;
                }

                if (updatedTenants.Any())
                {
                    _unitOfWork.Tenants.UpdateRange(updatedTenants.DistinctBy(t => t.Id));
                }

                _unitOfWork.Orders.UpdateRange(servedUnscannedOrders);
                await _unitOfWork.SaveAsync();
                await dbTxn.CommitAsync();

                _logger.LogInformation(
                    "Đã tính phí hoa hồng với tỷ lệ {CommissionRatePercent}% cho {TenantCount} tenant từ {OrderCount} đơn hàng ({ExemptCount} đơn miễn phí).",
                    commissionRatePercent,
                    updatedTenants.Select(t => t.Id).Distinct().Count(),
                    chargeableOrders.Count,
                    exemptOrders.Count);
            }
            catch (OperationCanceledException)
            {
                await dbTxn.RollbackAsync();
                _logger.LogWarning("CronJob CalculateWeeklyCommissionFeeAsync bị hủy.");
                throw;
            }
            catch (Exception ex)
            {
                await dbTxn.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi chạy CronJob: CalculateWeeklyCommissionFeeAsync");
            }

            _logger.LogInformation("Đã hoàn thành CronJob: CalculateWeeklyCommissionFeeAsync");
        }

        public async Task MonitorAndSuspendOverdueDebtsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: MonitorAndSuspendOverdueDebtsAsync vào lúc {Time}", DateTimeOffset.Now);

            await using var dbTxn = await _unitOfWork.BeginTransactionAsync();
            var suspendedTenantIds = new HashSet<Guid>();
            var receivingOrdersUpdates = new List<(int RestaurantId, bool IsReceiving)>();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tenants = await _unitOfWork.Tenants.GetAllAsync(
                    t => t.TotalDebtAmount > 0 && t.DebtStartedAt != null,
                    t => t.Account);

                if (!tenants.Any())
                {
                    await dbTxn.CommitAsync();
                    _logger.LogInformation("Không có tenant nợ phí hoa hồng cần theo dõi.");
                    return;
                }

                var tenantIds = tenants.Select(t => t.Id).ToList();
                var allRestaurants = await _unitOfWork.Restaurants.FindAsync(r => tenantIds.Contains(r.TenantId));
                var restaurantsByTenant = allRestaurants
                    .GroupBy(r => r.TenantId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var tenant in tenants)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var daysOverdue = (DateTime.UtcNow - tenant.DebtStartedAt!.Value).TotalDays;

                    if (daysOverdue >= 7)
                    {
                        var wasSuspended = tenant.IsSuspended;
                        tenant.IsSuspended = true;
                        suspendedTenantIds.Add(tenant.Id);

                        if (!wasSuspended && !string.IsNullOrWhiteSpace(tenant.Account?.Email))
                        {
                            var suspendedSubject = "Thong bao dinh chi tai khoan - ScanToOrder";
                            var suspendedHtmlContent = $@"
                                <h3>Kính gửi Quý khách hàng,</h3>
                                <p>Tài khoản của bạn đã bị <strong>dừng hoạt động</strong> do quá hạn thanh toán phí hoa hồng.</p>
                                <p>Số ngày quá hạn: <strong>{Math.Floor(daysOverdue)}</strong> ngày.</p>
                                <p>Tổng công nợ hiện tại: <strong>{tenant.TotalDebtAmount:N0} VND</strong>.</p>
                                <p>Vui lòng thanh toán để kích hoạt lại hệ thống và các nhà hàng.</p>
                                <p>Trân trọng,<br>ScanToOrder</p>";

                            await _emailService.SendEmailAsync(tenant.Account.Email, suspendedSubject, suspendedHtmlContent);
                        }

                        if (restaurantsByTenant.TryGetValue(tenant.Id, out var tenantRestaurants))
                        {
                            foreach (var restaurant in tenantRestaurants)
                            {
                                restaurant.IsActive = false;
                                restaurant.IsReceivingOrders = false;
                                receivingOrdersUpdates.Add((restaurant.Id, false));
                            }
                        }
                    }
                    else if (daysOverdue >= 3 && tenant.LastWarningSentAt == null)
                    {
                        tenant.LastWarningSentAt = DateTime.UtcNow;

                        if (!string.IsNullOrWhiteSpace(tenant.Account?.Email))
                        {
                            var subject = "Canh bao cong no phi hoa hong - ScanToOrder";
                            var htmlContent = $@"
                                <h3>Kính gửi Quý khách hàng,</h3>
                                <p>Tài khoản của bạn đang có công nợ phí hoa hồng cần thanh toán.</p>
                                <p>Số ngày quá hạn: <strong>{Math.Floor(daysOverdue)}</strong> ngày.</p>
                                <p>Tổng công nợ hiện tại: <strong>{tenant.TotalDebtAmount:N0} VND</strong>.</p>
                                <p>Vui lòng thanh toán sớm để tránh bị tạm ngưng hoạt động hệ thống.</p>
                                <p>Trân trọng,<br>ScanToOrder</p>";

                            await _emailService.SendEmailAsync(tenant.Account.Email, subject, htmlContent);
                        }
                    }
                    tenant.Account = null;
                }

                _unitOfWork.Tenants.UpdateRange(tenants);
                if (allRestaurants.Any())
                {
                    _unitOfWork.Restaurants.UpdateRange(allRestaurants);
                }

                await _unitOfWork.SaveAsync();
                await dbTxn.CommitAsync();

                foreach (var update in receivingOrdersUpdates.Distinct())
                {
                    await _realtimeService.NotifyReceivingOrdersChanged(update.RestaurantId.ToString(), update.IsReceiving);
                }

                foreach (var tenantId in suspendedTenantIds)
                {
                    await _realtimeService.NotifyTenantProfileChanged(tenantId.ToString());
                }

                _logger.LogInformation(
                    "Đã theo dõi công nợ: {TenantCount} tenant, trong đó {SuspendedCount} tenant bị tạm ngưng.",
                    tenants.Count,
                    suspendedTenantIds.Count);
            }
            catch (OperationCanceledException)
            {
                await dbTxn.RollbackAsync();
                _logger.LogWarning("CronJob MonitorAndSuspendOverdueDebtsAsync bị hủy.");
                throw;
            }
            catch (Exception ex)
            {
                await dbTxn.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi chạy CronJob: MonitorAndSuspendOverdueDebtsAsync");
            }

            _logger.LogInformation("Đã hoàn thành CronJob: MonitorAndSuspendOverdueDebtsAsync");
        }

        public async Task WarnUnpaidShiftsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: WarnUnpaidShiftsAsync vào lúc {Time}", DateTimeOffset.Now);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var unpaidReports = await _unitOfWork.ShiftReports.GetAllAsync(
                    r => r.IsTransferred == false && r.Shift.Status == ScanToOrder.Domain.Enums.ShiftStatus.Closed,
                    r => r.Shift.Staffs.Account,
                    r => r.Shift.Restaurants
                );

                if (!unpaidReports.Any())
                {
                    _logger.LogInformation("Không có ca nào chưa nộp tiền hợp lệ để cảnh báo.");
                    return;
                }

                var reportsByStaff = unpaidReports.GroupBy(r => r.Shift.StaffId);

                foreach (var group in reportsByStaff)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var firstReport = group.FirstOrDefault();
                    var staff = firstReport?.Shift?.Staffs;
                    var email = staff?.Account?.Email;

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        var totalDebt = group.Sum(r => r.TotalCashOrder);
                        
                        var shiftDetailsHtml = string.Join("", group.Select(r => 
                            $"<li>Ca <strong>#{r.ShiftId}</strong> (Nhà hàng: {r.Shift?.Restaurants?.RestaurantName ?? string.Empty}): Cần nộp <strong>{r.TotalCashOrder:N0} VNĐ</strong></li>"
                        ));

                        var subject = "Canh bao chua nop tien ca lam viec - ScanToOrder";
                        var htmlContent = $@"
                            <h3>Kính gửi {staff.Name},</h3>
                            <p>Tài khoản của bạn hiện đang có <strong>{group.Count()}</strong> ca trực đã kết thúc nhưng chưa hoàn tất việc nộp tiền mặt.</p>
                            <ul>
                                {shiftDetailsHtml}
                            </ul>
                            <p>Tổng số tiền nợ cần chuyển: <strong style='color:red;'>{totalDebt:N0} VNĐ</strong>.</p>
                            <p>Vui lòng tiến hành quét mã nộp tiền trên hệ thống đối với các ca chưa hoàn thành để hoàn tất đóng ca làm việc, tránh ảnh hưởng đến kiểm toán của nhà hàng.</p>
                            <p>Trân trọng,<br>ScanToOrder</p>";

                        await _emailService.SendEmailAsync(email, subject, htmlContent);
                    }
                }

                _logger.LogInformation("Đã kiểm tra và gửi cảnh báo dạng gộp cho {StaffCount} nhân viên với tổng cộng {ShiftCount} ca chưa nộp tiền.", reportsByStaff.Count(), unpaidReports.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("CronJob WarnUnpaidShiftsAsync bị hủy.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy CronJob: WarnUnpaidShiftsAsync");
            }

            _logger.LogInformation("Đã hoàn thành CronJob: WarnUnpaidShiftsAsync");
        }

        public async Task CalculateBestSellersAndAIEligibilityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: CalculateBestSellersAndAIEligibilityAsync vào lúc {Time}", DateTimeOffset.Now);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var restaurants = await _unitOfWork.Restaurants.FindAsync(r => r.IsActive == true);

                foreach (var restaurant in restaurants)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var orderCount = await _unitOfWork.Orders.CountAsync(o => o.RestaurantId == restaurant.Id && !o.IsDeleted);

                    bool isEligible = orderCount >= 50;
                    await _aiUpsellRedisService.SetAIEligibilityAsync(restaurant.Id, isEligible);

                    var bestSellers = await _unitOfWork.OrderDetails.QueryAsync(q => q
                        .Where(od => od.Order.RestaurantId == restaurant.Id && !od.Order.IsDeleted)
                        .GroupBy(od => od.DishId)
                        .Select(g => new { DishId = g.Key, TotalSold = g.Sum(x => x.Quantity) })
                        .OrderByDescending(x => x.TotalSold)
                        .Take(10)
                        .Select(x => x.DishId)
                    );

                    await _aiUpsellRedisService.SetBestSellersAsync(restaurant.Id, bestSellers);
                }

                _logger.LogInformation("Đã hoàn thành CalculateBestSellersAndAIEligibilityAsync cho {Count} nhà hàng.", System.Linq.Enumerable.Count(restaurants));
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("CronJob CalculateBestSellersAndAIEligibilityAsync bị hủy.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy CronJob: CalculateBestSellersAndAIEligibilityAsync");
            }
        }
}