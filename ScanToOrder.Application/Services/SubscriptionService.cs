using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScanToOrder.Application.DTOs.Payment;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Utils;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Application.Template;
using System.Text;

namespace ScanToOrder.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly IRealtimeService _realtimeService;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IRestaurantService _restaurantService;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        IUnitOfWork unitOfWork,
        IPaymentService paymentService,
        IRealtimeService realtimeService,
        IConfiguration configuration,
        IEmailService emailService,
        IRestaurantService restaurantService,
        ILogger<SubscriptionService> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _realtimeService = realtimeService;
        _configuration = configuration;
        _emailService = emailService;
        _restaurantService = restaurantService;
        _logger = logger;
    }

    public async Task<CheckoutPreviewResponse> CalculatePreviewAsync(PlanCheckoutRequest request, Guid currentTenantId)
    {
        var response = new CheckoutPreviewResponse();
        decimal totalAmount = 0;
        var restaurantIds = request.Items.Select(x => x.RestaurantId).Distinct().ToList();
        var targetPlanIds = request.Items.Select(x => x.TargetPlanId).Distinct().ToList();

        // Fetch all necessary data in bulk to minimize database calls
        var restaurantsDict = await _unitOfWork.Restaurants.GetByIdsWithTenantId(restaurantIds, currentTenantId);
        var targetPlansDict = await _unitOfWork.Plans.GetByIds(targetPlanIds);
        var currentSubsDict = await _unitOfWork.Subscriptions.GetByRestaurantIds(restaurantIds);

        // Calculate preview for each item in the request
        foreach (var item in request.Items)
        {
            // Skip if restaurant or target plan doesn't exist
            if (!restaurantsDict.TryGetValue(item.RestaurantId, out var restaurant)) continue;
            if (!targetPlansDict.TryGetValue(item.TargetPlanId, out var targetPlan)) continue;
            currentSubsDict.TryGetValue(item.RestaurantId, out var currentSub);

            // Block checkout for Trial plans — must be activated via ActivateTrialAsync
            if (targetPlan.IsTrial)
                throw new DomainException("Gói trải nghiệm không thể mua qua thanh toán. Vui lòng dùng tính năng kích hoạt gói trải nghiệm.");

            // Build the preview detail for this item
            var detail = new CheckoutPreviewItemResponse
            {
                RestaurantId = restaurant.Id,
                RestaurantName = restaurant.RestaurantName,
                TargetPlanName = targetPlan.Name,
                Cycle = item.Cycle,
                Quantity = item.Quantity
            };

            string cycleText = item.Cycle == BillingCycle.Yearly ? "năm" : "tháng";

            decimal basePrice = item.Cycle == BillingCycle.Yearly
                ? targetPlan.YearlyPrice * item.Quantity
                : targetPlan.MonthlyPrice * item.Quantity;

            detail.BasePrice = basePrice;

            // If there's no current active subscription (either first time or Expired and swept by CronJob), it's a new purchase
            if (currentSub == null)
            {
                detail.ActionType = SubscriptionLogStatus.BuyNew;
                detail.BalanceConverted = 0;
                detail.Message = $"Đăng ký mới {targetPlan.Name} ({item.Quantity} {cycleText}).";
            }
            // If current subscription is still active, determine if it's an upgrade, downgrade, or renewal
            else
            {
                var currentPlan = currentSub.Plan;

                // Upgrade: New plan has higher level than current plan
                if (targetPlan.Level > currentPlan.Level)
                {
                    detail.ActionType = SubscriptionLogStatus.Upgrade;

                    int remainingDays = (int)(currentSub.EndDate - DateTime.UtcNow).TotalDays;
                    if (remainingDays < 0) remainingDays = 0;

                    bool isOldPlanYearly = (currentSub.EndDate - currentSub.StartDate).TotalDays > 300;
                    decimal oldDailyRate = isOldPlanYearly ? currentPlan.DailyRateYear : currentPlan.DailyRateMonth;

                    detail.BalanceConverted = remainingDays * oldDailyRate;

                    decimal newDailyRate = item.Cycle == BillingCycle.Yearly
                        ? targetPlan.DailyRateYear
                        : targetPlan.DailyRateMonth;

                    int extraDays = 0;
                    if (newDailyRate > 0)
                    {
                        extraDays = (int)Math.Floor(detail.BalanceConverted / newDailyRate);
                    }

                    detail.Message =
                        $"Nâng cấp {targetPlan.Name}. Tiền dư {detail.BalanceConverted:N0}đ từ gói cũ được quy đổi thành {extraDays} ngày tặng thêm.";
                }
                // Downgrade: New plan has lower level than current plan
                else if (targetPlan.Level < currentPlan.Level)
                {
                    detail.ActionType = SubscriptionLogStatus.Downgrade;

                    int remainingDays = (int)(currentSub.EndDate - DateTime.UtcNow).TotalDays;
                    if (remainingDays < 0) remainingDays = 0;

                    bool isOldPlanYearly = (currentSub.EndDate - currentSub.StartDate).TotalDays > 300;
                    decimal oldDailyRate = isOldPlanYearly ? currentPlan.DailyRateYear : currentPlan.DailyRateMonth;

                    detail.BalanceConverted = remainingDays * oldDailyRate;

                    decimal newDailyRate = item.Cycle == BillingCycle.Yearly
                        ? targetPlan.DailyRateYear
                        : targetPlan.DailyRateMonth;

                    int extraDays = 0;
                    if (newDailyRate > 0)
                    {
                        extraDays = (int)Math.Floor(detail.BalanceConverted / newDailyRate);
                    }

                    detail.Message =
                        $"Hạ cấp xuống {targetPlan.Name}. Tiền dư {detail.BalanceConverted:N0}đ từ gói cũ được quy đổi thành {extraDays} ngày sử dụng.";
                }
                // Renewal: New plan is the same level as current plan
                else
                {
                    detail.ActionType = SubscriptionLogStatus.Renew;
                    detail.BalanceConverted = 0;

                    detail.Message = $"Gia hạn thêm {item.Quantity} {cycleText} cho gói {targetPlan.Name}.";
                }
            }

            detail.AmountToPay = basePrice;

            if (detail.AmountToPay < 0)
            {
                detail.AmountToPay = 0;
            }

            totalAmount += detail.AmountToPay;
            response.Details.Add(detail);
        }

        response.TotalAmountToPay = totalAmount;
        return response;
    }

    public async Task<string> CreatePaymentAsync(PlanCheckoutRequest request, Guid currentTenantId)
    {
        var previewResult = await CalculatePreviewAsync(request, currentTenantId);
        if (previewResult.TotalAmountToPay <= 0)
        {
            throw new InvalidOperationException(SubscriptionMessage.SubscriptionError.INVOICE_ZERO_CONTACT_ADMIN);
        }

        var payloadItems = previewResult.Details.Select(detail => new OrderPayloadItemPlan
        {
            RestaurantId = detail.RestaurantId,
            ActionType = detail.ActionType,
            NewPlanId = request.Items.First(i => i.RestaurantId == detail.RestaurantId).TargetPlanId,
            Cycle = detail.Cycle,
            Quantity = detail.Quantity,
            AmountAllocated = detail.AmountToPay,
            BalanceConverted = detail.BalanceConverted
        }).ToList();


        await using var dbTxn = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var transactionCode = BankQrLinkUtils.GeneratePayOsOrderCode();
            var transaction = new PaymentTransaction
            {
                TenantId = currentTenantId,
                TransactionCode = transactionCode.ToString(),
                PaymentDate = DateTime.UtcNow,
                TotalAmount = previewResult.TotalAmountToPay,
                Status = PaymentTransactionStatus.Pending
            };
            transaction.SetSubscriptionPayload(payloadItems);

            await _unitOfWork.PaymentTransactions.AddAsync(transaction);
            await _unitOfWork.SaveAsync();

            var paymentRequest = new CreatePaymentRequest()
            {
                OrderCode = transactionCode,
                Amount = (long)previewResult.TotalAmountToPay,
                CancelUrl = $"{GetFrontendBaseUrl()}/tenant/subscription-callback/cancel?orderCode={transactionCode}",
                ReturnUrl = $"{GetFrontendBaseUrl()}/tenant/subscription-callback/success?orderCode={transactionCode}",
                Description = $"Thanh toán dịch vụ S2O",
            };

            var paymentLink = await _paymentService.CreatePaymentLinkAsync(paymentRequest);
            await dbTxn.CommitAsync();
            return paymentLink;
        }
        catch (Exception ex)
        {
            await dbTxn.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi tạo link thanh toán subscription. TenantId: {TenantId}", currentTenantId);
            throw new DomainException(SubscriptionMessage.SubscriptionError.PAYMENT_SYSTEM_BUSY);
        }
    }

    public async Task<string> CreateCommissionFeePaymentAsync(Guid currentTenantId)
    {
        var tenant = (await _unitOfWork.Tenants.GetByIdAsync(currentTenantId))
            .OrThrow("Tenant không tồn tại");

        var configuration = (await _unitOfWork.Configurations.GetAllAsync()).FirstOrDefault();
        if (configuration == null || configuration.CommissionRate <= 0)
        {
            throw new DomainException("Cấu hình tỷ lệ hoa hồng không hợp lệ.");
        }

        if (tenant.TotalDebtAmount <= 0)
        {
            throw new DomainException("Không có khoản nợ hoa hồng nào cần thanh toán.");
        }

        await using var dbTxn = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var amountToPay = (long)Math.Ceiling(tenant.TotalDebtAmount);

            var transactionCode = BankQrLinkUtils.GeneratePayOsOrderCode();
            var transaction = new PaymentTransaction
            {
                TenantId = currentTenantId,
                TransactionCode = transactionCode.ToString(),
                PaymentDate = DateTime.UtcNow,
                TotalAmount = amountToPay,
                Status = PaymentTransactionStatus.Pending
            };

            var commissionRate = configuration.CommissionRate / 100m;
            var periodStart = tenant.DebtStartedAt.HasValue
                ? new DateTimeOffset(tenant.DebtStartedAt.Value, TimeSpan.Zero)
                : DateTimeOffset.UtcNow;

            var payload = new CommissionFeePayload
            {
                PeriodStart = periodStart,
                PeriodEnd = DateTimeOffset.UtcNow,
                CommissionRate = commissionRate,
                TotalOrderAmount = transaction.TotalAmount / commissionRate
            };
            transaction.SetCommissionPayload(payload);

            await _unitOfWork.PaymentTransactions.AddAsync(transaction);
            await _unitOfWork.SaveAsync();

            var paymentRequest = new CreatePaymentRequest
            {
                OrderCode = transactionCode,
                Amount = amountToPay,
                CancelUrl = $"{GetFrontendBaseUrl()}/tenant/debt-callback/cancel?orderCode={transactionCode}",
                ReturnUrl = $"{GetFrontendBaseUrl()}/tenant/debt-callback/success?orderCode={transactionCode}",
                Description = "Thanh toán nợ hoa hồng",
            };

            var paymentLink = await _paymentService.CreatePaymentLinkAsync(paymentRequest);
            await dbTxn.CommitAsync();
            return paymentLink;
        }
        catch (DomainException)
        {
            await dbTxn.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await dbTxn.RollbackAsync();
            throw new DomainException($"Không thể tạo link thanh toán nợ hoa hồng: {ex.Message}");
        }
    }

    public async Task MarkPaymentFailedAsync(long transactionCode)
    {
        var paymentTransaction = (await _unitOfWork.PaymentTransactions
                .FirstOrDefaultAsync(t => t.TransactionCode == transactionCode.ToString()))
            .OrThrow("Giao dịch không tồn tại");

        if (paymentTransaction.Status == PaymentTransactionStatus.Success ||
            paymentTransaction.Status == PaymentTransactionStatus.Canceled)
        {
            return;
        }

        paymentTransaction.Status = PaymentTransactionStatus.Failed;
        paymentTransaction.PaymentDate = DateTime.UtcNow;
        _unitOfWork.PaymentTransactions.Update(paymentTransaction);
        await _unitOfWork.SaveAsync();
    }

    public async Task MarkPaymentCanceledAsync(long transactionCode, Guid currentTenantId)
    {
        var paymentTransaction = (await _unitOfWork.PaymentTransactions
                .FirstOrDefaultAsync(t => t.TransactionCode == transactionCode.ToString()))
            .OrThrow("Giao dịch không tồn tại");

        if (paymentTransaction.TenantId != currentTenantId)
        {
            throw new DomainException(SubscriptionMessage.SubscriptionError.NO_PERMISSION_TO_UPDATE_TRANSACTION);
        }

        if (paymentTransaction.Status == PaymentTransactionStatus.Success)
        {
            return;
        }

        paymentTransaction.Status = PaymentTransactionStatus.Canceled;
        paymentTransaction.PaymentDate = DateTime.UtcNow;
        _unitOfWork.PaymentTransactions.Update(paymentTransaction);
        await _unitOfWork.SaveAsync();
    }

    public async Task<PaymentStatusResponse> GetPaymentStatusAsync(long transactionCode, Guid currentTenantId)
    {
        var paymentTransaction = (await _unitOfWork.PaymentTransactions
                .FirstOrDefaultAsync(t => t.TransactionCode == transactionCode.ToString()))
            .OrThrow("Giao dịch không tồn tại");

        if (paymentTransaction.TenantId != currentTenantId)
        {
            throw new DomainException(SubscriptionMessage.SubscriptionError.NO_PERMISSION_TO_VIEW_TRANSACTION);
        }

        if (paymentTransaction.PaymentTransactionType == PaymentTransactionType.Subscription &&
            paymentTransaction.Status == PaymentTransactionStatus.Success &&
            !await HasProcessedSubscriptionPaymentAsync(paymentTransaction.Id))
        {
            await ProcessPaymentSuccessAsync(transactionCode);

            paymentTransaction = (await _unitOfWork.PaymentTransactions
                    .FirstOrDefaultAsync(t => t.TransactionCode == transactionCode.ToString()))
                .OrThrow("Giao dịch không tồn tại");
        }

        if (paymentTransaction.PaymentTransactionType == PaymentTransactionType.Subscription &&
            paymentTransaction.Status == PaymentTransactionStatus.Pending)
        {
            var isPaidOnGateway = await _paymentService.IsPaymentSuccessfulAsync(transactionCode);
            if (isPaidOnGateway)
            {
                await ProcessPaymentSuccessAsync(transactionCode);

                paymentTransaction = (await _unitOfWork.PaymentTransactions
                        .FirstOrDefaultAsync(t => t.TransactionCode == transactionCode.ToString()))
                    .OrThrow("Giao dịch không tồn tại");
            }
        }

        var isFinal = paymentTransaction.Status == PaymentTransactionStatus.Success ||
                      paymentTransaction.Status == PaymentTransactionStatus.Failed ||
                      paymentTransaction.Status == PaymentTransactionStatus.Canceled;

        return new PaymentStatusResponse
        {
            OrderCode = transactionCode,
            TotalAmount = paymentTransaction.TotalAmount,
            Status = paymentTransaction.Status.ToString(),
            IsFinal = isFinal,
            LastUpdatedAt = paymentTransaction.UpdatedAt ?? paymentTransaction.PaymentDate
        };
    }

    private string GetFrontendBaseUrl()
    {
        var localBaseUrl = _configuration["FrontEndUrl:local"];
        if (!string.IsNullOrWhiteSpace(localBaseUrl))
        {
            return localBaseUrl.TrimEnd('/');
        }

        var baseUrl = _configuration["FrontEndUrl:scan2order_id_vn"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:3000";
        }

        return baseUrl.TrimEnd('/');
    }

    public async Task ProcessPaymentSuccessAsync(long transactionCode)
    {
        // Get transaction and ensure it exists
        var paymentTransaction =
            (await _unitOfWork.PaymentTransactions.FirstOrDefaultAsync(t =>
                t.TransactionCode == transactionCode.ToString()))
            .OrThrow("Giao dịch không tồn tại");

        switch (paymentTransaction.PaymentTransactionType)
        {
            case PaymentTransactionType.Subscription:
                if (await HasProcessedSubscriptionPaymentAsync(paymentTransaction.Id))
                {
                    if (paymentTransaction.Status != PaymentTransactionStatus.Success)
                    {
                        paymentTransaction.Status = PaymentTransactionStatus.Success;
                        paymentTransaction.PaymentDate = DateTime.UtcNow;
                        _unitOfWork.PaymentTransactions.Update(paymentTransaction);
                        await _unitOfWork.SaveAsync();
                    }

                    return;
                }

                await ProcessSubscriptionSuccessAsync(paymentTransaction);
                break;
            case PaymentTransactionType.CommissionFee:
                if (paymentTransaction.Status == PaymentTransactionStatus.Success) return;

                await ProcessCommissionFeeSuccessAsync(paymentTransaction);
                break;
            default:
                throw new DomainException("Loại giao dịch thanh toán không hợp lệ");
        }
    }

    private async Task<bool> HasProcessedSubscriptionPaymentAsync(int paymentTransactionId)
    {
        return await _unitOfWork.SubscriptionLogs.ExistsAsync(log =>
            log.PaymentTransactionId == paymentTransactionId);
    }

    private async Task ProcessSubscriptionSuccessAsync(PaymentTransaction paymentTransaction)
    {
        var payload = paymentTransaction.SubscriptionPayload;
        if (payload == null || !payload.Any()) return;

        var restaurantIds = payload.Select(x => x.RestaurantId).Distinct().ToList();
        var newPlanIds = payload.Select(x => x.NewPlanId).Distinct().ToList();

        // Query 1: Fetch tracked subscriptions for the target restaurants.
        // Include expired rows so we can reactivate them instead of inserting duplicates.
        var currentSubs = await _unitOfWork.Subscriptions.FindAsync(s =>
            restaurantIds.Contains(s.RestaurantId) && !s.IsDeleted);
        var currentSubsDict = currentSubs
            .GroupBy(s => s.RestaurantId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EndDate).First());

        // Query 2: Fetch all target plans' details using Custom Repository Method
        var plansDict = await _unitOfWork.Plans.GetByIds(newPlanIds);

        // Query 3: Fetch the default menu template
        var defaultTemplate = await _unitOfWork.MenuTemplates.FirstOrDefaultAsync(t => t.IsDefault);
        int fallbackTemplateId = defaultTemplate?.Id ?? 12;

        // Begin database transaction to ensure data integrity
        await using var dbTxn = await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Process each item in the payload to update subscriptions and create logs
            foreach (var item in payload)
            {
                // RETRIEVE DATA FROM RAM (Dictionary) INSTEAD OF CALLING THE DATABASE
                currentSubsDict.TryGetValue(item.RestaurantId, out var currentSub);

                // Skip or throw error if the target plan doesn't exist
                if (!plansDict.TryGetValue(item.NewPlanId, out var targetPlan))
                {
                    throw new DomainException($"Gói dịch vụ không tìm thấy (ID: {item.NewPlanId}). Vui lòng liên hệ bộ phận hỗ trợ để được xử lý.");
                }

                // Calculate base days to add based on the billing cycle and purchased quantity
                int baseDays = item.Cycle == BillingCycle.Yearly
                    ? 365 * item.Quantity
                    : 30 * item.Quantity;

                // Calculate extra days from balance conversion (Time-based Proration) if applicable
                int extraDays = 0;
                if (item.BalanceConverted > 0)
                {
                    decimal newDailyRate = item.Cycle == BillingCycle.Yearly
                        ? targetPlan.DailyRateYear
                        : targetPlan.DailyRateMonth;

                    if (newDailyRate > 0)
                    {
                        extraDays = (int)Math.Floor(item.BalanceConverted / newDailyRate);
                    }
                }

                // Total days to add is the sum of base purchased days and extra converted days
                int totalDaysToAdd = baseDays + extraDays;

                // Initialize temporal variables to accurately record the subscription log
                DateTime now = DateTime.UtcNow;
                DateTime? oldExpiredDate = currentSub?.EndDate;
                DateTime newExpiredDate = now;

                // Handle different action types: BuyNew, Upgrade, Downgrade, Renew
                if (item.ActionType == SubscriptionLogStatus.BuyNew || currentSub == null ||
                    currentSub.Status == SubscriptionStatus.Expired)
                {
                    // BuyNew: Start counting from today
                    newExpiredDate = now.AddDays(totalDaysToAdd);

                    if (currentSub == null)
                    {
                        var newSub = new Subscription
                        {
                            RestaurantId = item.RestaurantId,
                            PlanId = item.NewPlanId,
                            StartDate = now,
                            EndDate = newExpiredDate,
                            Status = SubscriptionStatus.Active
                        };
                        await _unitOfWork.Subscriptions.AddAsync(newSub);
                    }
                    else
                    {
                        // Reuse existing expired row to avoid unique RestaurantId violations.
                        currentSub.PlanId = item.NewPlanId;
                        currentSub.StartDate = now;
                        currentSub.EndDate = newExpiredDate;
                        currentSub.Status = SubscriptionStatus.Active;
                        _unitOfWork.Subscriptions.Update(currentSub);
                    }
                }
                else if (item.ActionType == SubscriptionLogStatus.Upgrade ||
                         item.ActionType == SubscriptionLogStatus.Downgrade)
                {
                    // Upgrades & Downgrades: Replace the current plan immediately and apply the new duration
                    newExpiredDate = now.AddDays(totalDaysToAdd);

                    currentSub.PlanId = item.NewPlanId;
                    currentSub.StartDate = now;
                    currentSub.EndDate = newExpiredDate;
                    _unitOfWork.Subscriptions.Update(currentSub);
                }
                else if (item.ActionType == SubscriptionLogStatus.Renew)
                {
                    // Renewals: Extend the current end date. If already expired, start from today.
                    DateTime baseDate = currentSub.EndDate > now ? currentSub.EndDate : now;
                    newExpiredDate = baseDate.AddDays(totalDaysToAdd);

                    currentSub.EndDate = newExpiredDate;
                    _unitOfWork.Subscriptions.Update(currentSub);
                }

                // Handle Resetting Menu Template on Downgrade or BuyNew
                if (item.ActionType == SubscriptionLogStatus.Downgrade || item.ActionType == SubscriptionLogStatus.BuyNew || currentSub == null)
                {
                    var menuRestaurant = await _unitOfWork.MenuRestaurants
                        .FirstOrDefaultAsync(mr => mr.RestaurantId == item.RestaurantId);
                    
                    if (menuRestaurant != null && menuRestaurant.MenuTemplateId != fallbackTemplateId)
                    {
                        menuRestaurant.MenuTemplateId = fallbackTemplateId;
                        _unitOfWork.MenuRestaurants.Update(menuRestaurant);
                    }
                }

                // Create a subscription log entry to securely track historical changes
                var log = new SubscriptionLog
                {
                    RestaurantId = item.RestaurantId,
                    ActionType = item.ActionType,

                    OldPlanId = item.OldPlanId,
                    NewPlanId = item.NewPlanId,

                    AmountAllocated = item.AmountAllocated,
                    BalanceConvereted = item.BalanceConverted,
                    PaymentTransactionId = paymentTransaction.Id,
                    DaysAdded = totalDaysToAdd,

                    OldExpired = oldExpiredDate,
                    NewExpired = newExpiredDate,

                    CreatedAt = now
                };
                await _unitOfWork.SubscriptionLogs.AddAsync(log);
            }

            // Mark the overarching transaction as fully successful
            paymentTransaction.Status = PaymentTransactionStatus.Success;
            paymentTransaction.PaymentDate = DateTime.UtcNow;
            _unitOfWork.PaymentTransactions.Update(paymentTransaction);
            
            // Persist all modifications to the database in one single batch
            await _unitOfWork.SaveAsync();
            await dbTxn.CommitAsync();

            await _realtimeService.NotifySubscriptionChanged(paymentTransaction.TenantId.ToString());
            await _realtimeService.NotifyTenantProfileChanged(paymentTransaction.TenantId.ToString());
        }
        catch (Exception ex)
        {
            await dbTxn.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi cập nhật gói dịch vụ sau thanh toán. TransactionId: {TransactionId}", paymentTransaction.Id);
            throw new DomainException("Cập nhật gói dịch vụ thất bại. Giao dịch đã được hoàn tác và tiền của bạn không bị trừ. Vui lòng liên hệ bộ phận hỗ trợ.");
        }
    }

    private async Task ProcessCommissionFeeSuccessAsync(PaymentTransaction paymentTransaction)
    {
        var payload = paymentTransaction.CommissionPayload;
        if (payload == null) return;

        await using var dbTxn = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var tenant = (await _unitOfWork.Tenants.GetByIdAsync(paymentTransaction.TenantId))
                .OrThrow("Tenant không tồn tại");

            var wasSuspended = tenant.IsSuspended;

            tenant.TotalDebtAmount = 0;
            tenant.DebtStartedAt = null;
            tenant.LastWarningSentAt = null;
            if (wasSuspended)
            {
                tenant.IsSuspended = false;
                tenant.SuspendedAt = null;
            }
            _unitOfWork.Tenants.Update(tenant);

            var restaurants = await _unitOfWork.Restaurants.GetAllAsync(r => r.TenantId == paymentTransaction.TenantId);
            foreach (var restaurant in restaurants)
            {
                restaurant.IsActive = true;
                restaurant.IsReceivingOrders = true;
            }
            _unitOfWork.Restaurants.UpdateRange(restaurants);

            paymentTransaction.Status = PaymentTransactionStatus.Success;
            paymentTransaction.PaymentDate = DateTime.UtcNow;
            _unitOfWork.PaymentTransactions.Update(paymentTransaction);

            await _unitOfWork.SaveAsync();
            await dbTxn.CommitAsync();

            foreach (var restaurant in restaurants)
            {
                await _realtimeService.NotifyReceivingOrdersChanged(restaurant.Id.ToString(), true);
            }

            await _realtimeService.NotifyTenantProfileChanged(paymentTransaction.TenantId.ToString());
        }
        catch (Exception ex)
        {
            await dbTxn.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi xử lý thanh toán hoa hồng. TransactionId: {TransactionId}", paymentTransaction.Id);
            throw new DomainException("Thanh toán hoa hồng thất bại. Giao dịch đã được hoàn tác. Vui lòng thử lại hoặc liên hệ bộ phận hỗ trợ.");
        }
    }

    public async Task<List<RestaurantSubscriptionDto>> GetSubscriptionsByTenantAsync(Guid tenantId)
    {
        var restaurants = await _unitOfWork.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId);
        
        var result = restaurants.Select(r =>
        {
            var currentSub = r.Subscription;

            var dto = new RestaurantSubscriptionDto
            {
                RestaurantId = r.Id,
                RestaurantName = r.RestaurantName,
                Address = r.Address ?? string.Empty,
                IsActive = r.IsActive ?? false
            };

            if (currentSub != null)
            {
                dto.CurrentSubscriptionId = currentSub.Id;
                dto.CurrentPlanId = currentSub.PlanId;
                dto.CurrentPlanName = currentSub.Plan?.Name;
                dto.StartDate = currentSub.StartDate;
                dto.EndDate = currentSub.EndDate;
                dto.Status = currentSub.Status.ToString();
                dto.IsTrialPlan = currentSub.Plan?.IsTrial ?? false;
            }

            return dto;
        }).ToList();

        return result;
    }

    public async Task<List<PaymentTransactionHistoryDto>> GetPaymentTransactionsByTenantAsync(Guid tenantId)
    {
        var transactions = await _unitOfWork.PaymentTransactions.GetAllAsync(
            pt => pt.TenantId == tenantId
        );

        // Fetch dependent data for mapping names
        var restaurantIds = new HashSet<int>();
        var planIds = new HashSet<int>();

        foreach (var txn in transactions)
        {
            if (txn.SubscriptionPayload != null)
            {
                foreach (var item in txn.SubscriptionPayload)
                {
                    restaurantIds.Add(item.RestaurantId);
                    planIds.Add(item.NewPlanId);
                    if (item.OldPlanId.HasValue)
                        planIds.Add(item.OldPlanId.Value);
                }
            }
        }

        var restaurantsMap = (await _unitOfWork.Restaurants.FindAsync(r => restaurantIds.Contains(r.Id)))
            .ToDictionary(r => r.Id, r => r.RestaurantName);
            
        var plansMap = (await _unitOfWork.Plans.FindAsync(p => planIds.Contains(p.Id)))
            .ToDictionary(p => p.Id, p => p.Name);

        var result = new List<PaymentTransactionHistoryDto>();

        foreach (var pt in transactions.OrderByDescending(x => x.PaymentDate))
        {
            var dto = new PaymentTransactionHistoryDto
            {
                Id = pt.Id,
                TransactionCode = pt.TransactionCode,
                TotalAmount = pt.TotalAmount,
                PaymentDate = pt.PaymentDate,
                Status = pt.Status.ToString(),
                PaymentTransactionType = pt.PaymentTransactionType.ToString(),
                CommissionDetails = pt.CommissionPayload
            };

            if (pt.SubscriptionPayload != null)
            {
                dto.SubscriptionDetails = new List<SubscriptionTransactionItemDto>();
                foreach (var sPayload in pt.SubscriptionPayload)
                {
                    restaurantsMap.TryGetValue(sPayload.RestaurantId, out var rName);
                    plansMap.TryGetValue(sPayload.NewPlanId, out var newPlanName);
                    string? oldPlanName = null;
                    if (sPayload.OldPlanId.HasValue)
                        plansMap.TryGetValue(sPayload.OldPlanId.Value, out oldPlanName);

                    string cycleStr = sPayload.Cycle == Domain.Enums.BillingCycle.Monthly ? "tháng" : "năm";
                    string desc = sPayload.ActionType switch
                    {
                        Domain.Enums.SubscriptionLogStatus.BuyNew => $"Đăng ký mới gói {newPlanName} ({sPayload.Quantity} {cycleStr})",
                        Domain.Enums.SubscriptionLogStatus.Upgrade => $"Nâng cấp lên gói {newPlanName} ({sPayload.Quantity} {cycleStr}). Tiền dư {sPayload.BalanceConverted:N0}đ được chuyển thành ngày tặng thêm.",
                        Domain.Enums.SubscriptionLogStatus.Downgrade => $"Hạ cấp xuống gói {newPlanName} ({sPayload.Quantity} {cycleStr}). Tiền dư {sPayload.BalanceConverted:N0}đ được chuyển thành ngày sử dụng.",
                        Domain.Enums.SubscriptionLogStatus.Renew => $"Gia hạn gói {newPlanName} thêm {sPayload.Quantity} {cycleStr}.",
                        _ => "Cập nhật dịch vụ"
                    };

                    dto.SubscriptionDetails.Add(new SubscriptionTransactionItemDto
                    {
                        RestaurantId = sPayload.RestaurantId,
                        RestaurantName = rName ?? "Không xác định",
                        ActionType = sPayload.ActionType.ToString(),
                        OldPlanId = sPayload.OldPlanId,
                        OldPlanName = oldPlanName,
                        NewPlanId = sPayload.NewPlanId,
                        NewPlanName = newPlanName ?? "Không xác định",
                        Cycle = sPayload.Cycle.ToString(),
                        Quantity = sPayload.Quantity,
                        AmountAllocated = sPayload.AmountAllocated,
                        BalanceConverted = sPayload.BalanceConverted,
                        DescriptionMessage = desc
                    });
                }
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task ProcessSubscriptionExpirationsAsync()
    {
        var utcNow = DateTime.UtcNow;

        // 1. Query expired subscriptions — include Plan to check IsTrial
        var expiredSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync(
            s => s.Status == SubscriptionStatus.Active && s.EndDate <= utcNow,
            s => s.Restaurant,
            s => s.Restaurant.Tenant,
            s => s.Restaurant.Tenant.Account,
            s => s.Plan
        );

        if (!expiredSubscriptions.Any()) return;

        // 2. Split into Trial vs Normal
        var trialExpired = expiredSubscriptions.Where(s => s.Plan?.IsTrial == true).ToList();
        var normalExpired = expiredSubscriptions.Where(s => s.Plan?.IsTrial != true).ToList();

        // NORMAL SUBSCRIPTIONS: send email + set Expired 
        if (normalExpired.Any())
        {
            var normalIds = normalExpired.Select(s => s.Id).ToList();
            var normalRestaurantIds = normalExpired
                .Where(s => s.Restaurant != null)
                .Select(s => s.Restaurant.Id).Distinct().ToList();

            // 3. Send expired notification emails (grouped by tenant)
            var expiredEmailGroups = normalExpired
                .Where(s => !string.IsNullOrEmpty(s.Restaurant?.Tenant?.Account?.Email))
                .GroupBy(s => s.Restaurant.Tenant.Account.Email);

            foreach (var group in expiredEmailGroups)
            {
                var email = group.Key;
                var restaurantListHtml = BuildRestaurantListHtml(group);

                await _emailService.SendEmailWithTemplateIdDomainAsync(
                    email,
                    "Thông báo: Gói dịch vụ đã hết hạn - ScanToOrder",
                    ResendTemplate.EXPIRED_SUBSCRIPTION_TEMPLATE_ID,
                    new
                    {
                        restaurant_list = $"<ul>{restaurantListHtml}</ul>",
                        admin_url = "https://admin.scantoorder.com"
                    }
                );
            }

            // 4. Batch update: set Expired
            var subscriptionsToUpdate = await _unitOfWork.Subscriptions.FindAsync(
                s => normalIds.Contains(s.Id));
            foreach (var sub in subscriptionsToUpdate)
                sub.Status = SubscriptionStatus.Expired;
            _unitOfWork.Subscriptions.UpdateRange(subscriptionsToUpdate);

            // 5. Deactivate restaurants
            var restaurantsToDeactivate = await _unitOfWork.Restaurants.FindAsync(
                r => normalRestaurantIds.Contains(r.Id));
            foreach (var restaurant in restaurantsToDeactivate)
            {
                restaurant.IsActive = false;
                restaurant.IsOpened = false;
                restaurant.IsReceivingOrders = false;
            }
            _unitOfWork.Restaurants.UpdateRange(restaurantsToDeactivate);
        }

        // TRIAL SUBSCRIPTIONS: hard delete + deactivate
        if (trialExpired.Any())
        {
            var trialIds = trialExpired.Select(s => s.Id).ToList();
            var trialRestaurantIds = trialExpired
                .Where(s => s.Restaurant != null)
                .Select(s => s.Restaurant.Id).Distinct().ToList();

            // Hard delete trial subscriptions (row is removed, SubscriptionLog stays intact)
            var trialSubsToDelete = await _unitOfWork.Subscriptions.FindAsync(
                s => trialIds.Contains(s.Id));
            _unitOfWork.Subscriptions.RemoveRange(trialSubsToDelete);

            // Deactivate trial restaurants
            var trialRestaurants = await _unitOfWork.Restaurants.FindAsync(
                r => trialRestaurantIds.Contains(r.Id));
            foreach (var restaurant in trialRestaurants)
            {
                restaurant.IsActive = false;
                restaurant.IsOpened = false;
                restaurant.IsReceivingOrders = false;
            }
            _unitOfWork.Restaurants.UpdateRange(trialRestaurants);
        }

        // 7. Single SaveAsync for all changes
        await _unitOfWork.SaveAsync();

        // 8. Send warning emails for subscriptions expiring within 24h
        var tomorrow = utcNow.AddDays(1);
        var expiringSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync(
            s => s.Status == SubscriptionStatus.Active && s.EndDate > utcNow && s.EndDate <= tomorrow,
            s => s.Restaurant,
            s => s.Restaurant.Tenant,
            s => s.Restaurant.Tenant.Account
        );

        var expiringEmailGroups = expiringSubscriptions
            .Where(s => !string.IsNullOrEmpty(s.Restaurant?.Tenant?.Account?.Email))
            .GroupBy(s => s.Restaurant.Tenant.Account.Email);

        foreach (var group in expiringEmailGroups)
        {
            var email = group.Key;
            var restaurantListHtml = BuildRestaurantListHtml(group);

            await _emailService.SendEmailWithTemplateIdDomainAsync(
                email,
                "Sắp hết hạn gói dịch vụ - Vui lòng gia hạn",
                ResendTemplate.EXPIRING_SUBSCRIPTION_TEMPLATE_ID,
                new
                {
                    restaurant_list = $"<ul>{restaurantListHtml}</ul>",
                    admin_url = "https://scan2order.io.vn"
                }
            );
        }
    }

    private string BuildRestaurantListHtml(IEnumerable<Subscription> subs)
    {
        var sb = new StringBuilder();

        foreach (var sub in subs)
        {
            sb.Append(
                $"<li><strong>{sub.Restaurant.RestaurantName}</strong> " +
                $"(Thời điểm hết hạn: {sub.EndDate.ToLocalTime():dd/MM/yyyy HH:mm})</li>"
            );
        }

        return sb.ToString();
    }

    public async Task ActivateTrialAsync(int restaurantId, Guid currentTenantId)
    {
        // 1. Check Tenant has not used trial before
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(currentTenantId)
            ?? throw new DomainException("Không tìm thấy thông tin tài khoản.");

        if (tenant.HasUsedTrial)
            throw new DomainException("Tài khoản của bạn đã sử dụng gói trải nghiệm trước đó và không thể kích hoạt lại.");

        // 2. Validate restaurant belongs to this tenant
        var restaurant = await _unitOfWork.Restaurants.GetByFieldsIncludeAsync(
            r => r.Id == restaurantId && r.TenantId == currentTenantId,
            r => r.Subscription
        ) ?? throw new DomainException("Không tìm thấy nhà hàng hoặc bạn không có quyền truy cập.");

        // 3. Restaurant must not already have an active subscription
        if (restaurant.Subscription != null && restaurant.Subscription.Status == SubscriptionStatus.Active)
            throw new DomainException("Nhà hàng này đang có gói dịch vụ hoạt động. Vui lòng hủy hoặc chờ hết hạn trước khi kích hoạt gói trải nghiệm.");

        // 4. Find the Trial plan (auto-detect — Admin creates it with IsTrial = true)
        var trialPlan = await _unitOfWork.Plans.GetByFieldsIncludeAsync(
            p => p.IsTrial && p.Status == PlanStatus.Active
        ) ?? throw new DomainException("Hệ thống chưa cấu hình gói trải nghiệm. Vui lòng liên hệ Admin.");

        var now = DateTime.UtcNow;
        var endDate = now.AddDays(trialPlan.DurationInDays);

        // 5. Create the Subscription
        var subscription = new Subscription
        {
            RestaurantId = restaurantId,
            PlanId = trialPlan.Id,
            StartDate = now,
            EndDate = endDate,
            Status = SubscriptionStatus.Active
        };
        await _unitOfWork.Subscriptions.AddAsync(subscription);

        // 6. Write SubscriptionLog (no PaymentTransaction — free, nullable FK)
        var log = new SubscriptionLog
        {
            RestaurantId = restaurantId,
            NewPlanId = trialPlan.Id,
            OldPlanId = null,
            PaymentTransactionId = null,
            ActionType = SubscriptionLogStatus.Trial,
            AmountAllocated = 0,
            BalanceConvereted = 0,
            DaysAdded = trialPlan.DurationInDays,
            OldExpired = null,
            NewExpired = endDate,
            CreatedAt = now
        };
        await _unitOfWork.SubscriptionLogs.AddAsync(log);

        // 7. Mark tenant as used trial
        tenant.HasUsedTrial = true;
        _unitOfWork.Tenants.Update(tenant);

        // 8. Activate the restaurant
        restaurant.IsActive = true;
        _unitOfWork.Restaurants.Update(restaurant);

        await _unitOfWork.SaveAsync();

        // 9. Notify via SignalR
        var tenantIdStr = currentTenantId.ToString();
        await _realtimeService.SendNotificationToTenant(tenantIdStr, new
        {
            type = "SubscriptionChanged",
            message = $"Gói trải nghiệm đã được kích hoạt cho nhà hàng. Thời hạn: {endDate.ToLocalTime():dd/MM/yyyy HH:mm}."
        });
        await _realtimeService.SendNotificationToTenant(tenantIdStr, new
        {
            type = "ProfileChanged"
        });
    }
}
