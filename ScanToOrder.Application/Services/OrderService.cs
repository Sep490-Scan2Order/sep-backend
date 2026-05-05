using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using DocumentFormat.OpenXml.InkML;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Utils;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartRedisService _cartRedisService;
    private readonly ITransactionRedisService _transactionRedisService;
    private readonly IRealtimeService _realtimeService;
    private readonly IMenuCacheService _menuCacheService;
    private readonly IMapper _mapper;
    private readonly IAuthenticatedUserService _authenticatedUserService;
    private readonly IStorageService _storageService;
    private readonly ILogger<OrderService> _logger;
    private readonly IQrCodeService _qrCodeService;
    private readonly IPlanLimitationService _planLimitationService;
    private readonly IAIUpsellService _aiUpsellService;
    private readonly IBackgroundJobService _backgroundJobService;
    
    public OrderService(
        IUnitOfWork unitOfWork,
        ICartRedisService cartRedisService,
        ITransactionRedisService transactionRedisService,
        IRealtimeService realtimeService,
        IMenuCacheService menuCacheService,
        IMapper mapper,
        IAuthenticatedUserService authenticatedUserService,
        IStorageService storageService,
        ILogger<OrderService> logger,
        IQrCodeService qrCodeService,
        IPlanLimitationService planLimitationService,
        IAIUpsellService aiUpsellService,
        IBackgroundJobService backgroundJobService)
    {
        _unitOfWork = unitOfWork;
        _cartRedisService = cartRedisService;
        _transactionRedisService = transactionRedisService;
        _realtimeService = realtimeService;
        _menuCacheService = menuCacheService;
        _mapper = mapper;
        _authenticatedUserService = authenticatedUserService;
        _storageService = storageService;
        _logger = logger;
        _qrCodeService = qrCodeService;
        _planLimitationService = planLimitationService;
        _aiUpsellService = aiUpsellService;
        _backgroundJobService = backgroundJobService;
    }

    public async Task<CartDto> AddToCartAsync(AddToCartRequest request)
    {
        if (request.Quantity <= 0)
        {
            throw new DomainException(OrderMessage.OrderError.QUANTITY_MUST_BE_GREATER_THAN_ZERO);
        }

        // gộp truy vấn config và món ăn thành 1 lần để giảm tải db
        var branchDish = await _unitOfWork.BranchDishConfigs.GetByFieldsIncludeAsync(
            b => b.RestaurantId == request.RestaurantId
              && b.DishId == request.DishId
              && !b.IsDeleted,
            b => b.Dish
        );

        if (branchDish == null)
        {
            throw new DomainException(DishMessage.DishError.DISH_NOT_FOUND);
        }

        if (!branchDish.IsSelling)
        {
            throw new DomainException(BranchDishMessage.BranchDishError.NOT_SELLING);
        }

        if (branchDish.IsSoldOut)
        {
            throw new DomainException(BranchDishMessage.BranchDishError.SOLD_OUT);
        }

        var dish = branchDish.Dish;

        // 4. Xác định Cart hiện tại hoặc tạo mới
        var cartId = string.IsNullOrWhiteSpace(request.CartId)
            ? Guid.NewGuid().ToString("N")
            : request.CartId;

        CartModel cart;

        var existingJson = await _cartRedisService.GetRawCartAsync(cartId);
        if (!string.IsNullOrEmpty(existingJson))
        {
            cart = JsonSerializer.Deserialize<CartModel>(existingJson) ?? new CartModel
            {
                CartId = cartId,
                RestaurantId = request.RestaurantId
            };

            if (cart.RestaurantId != request.RestaurantId)
            {
                throw new DomainException(OrderMessage.OrderError.CANNOT_ADD_DISH_FROM_OTHER_RESTAURANT);
            }
        }
        else
        {
            cart = new CartModel
            {
                CartId = cartId,
                RestaurantId = request.RestaurantId
            };
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            cart.Note = request.Note;
        }

        // 5. Thêm hoặc cập nhật item trong cart
        var existingItem = cart.Items.FirstOrDefault(i => i.DishId == request.DishId);
        if (existingItem == null)
        {
            existingItem = new CartItemModel
            {
                DishId = request.DishId,
                DishName = dish.DishName,
                Quantity = request.Quantity,
                DiscountedPrice = branchDish.Price,
                OriginalPrice = branchDish.Price,
                SubTotal = branchDish.Price * request.Quantity
            };
            cart.Items.Add(existingItem);
        }
        else
        {
            existingItem.Quantity += request.Quantity;
            existingItem.SubTotal = existingItem.DiscountedPrice * existingItem.Quantity;
        }

        // 6. Tính lại tổng tiền giỏ
        cart.TotalAmount = cart.Items.Sum(i => i.SubTotal);

        // 7. Lưu lại cart lên Redis dưới dạng JSON string
        var json = JsonSerializer.Serialize(cart);
        await _cartRedisService.SaveRawCartAsync(cartId, json, TimeSpan.FromMinutes(60));

        // 8. Đồng bộ lại giá/khuyến mãi/tồn kho trước khi trả về
        cart = await SyncCartPricingAndAvailabilityAsync(cart);

        // 9. Trả về full CartDto 
        var cartDto = _mapper.Map<CartDto>(cart);

        return cartDto;
    }

    public async Task<List<MenuDishItemDto>> GetCartRecommendationsAsync(string cartId)
    {
        if (string.IsNullOrWhiteSpace(cartId))
        {
            throw new DomainException(OrderMessage.OrderError.CART_ID_REQUIRED);
        }

        var json = await _cartRedisService.GetRawCartAsync(cartId);
        if (string.IsNullOrEmpty(json))
        {
            throw new DomainException(OrderMessage.OrderError.CART_NOT_FOUND_OR_EXPIRED);
        }

        var cart = JsonSerializer.Deserialize<CartModel>(json)
                   ?? throw new DomainException(OrderMessage.OrderError.INVALID_CART_DATA);

        var cartDishIds = cart.Items.Select(x => x.DishId).ToList();

        var (recommendedIds, source) = await _aiUpsellService.GetRecommendationsAsync(cart.RestaurantId, cartDishIds, 3);
        
        if (recommendedIds != null && recommendedIds.Any())
        {
            return await GetDishesByIdsWithPromotionAsync(cart.RestaurantId, recommendedIds);
        }

        return new List<MenuDishItemDto>();
    }

    public async Task<CartDto> GetCartAsync(string cartId)
    {
        if (string.IsNullOrWhiteSpace(cartId))
        {
            throw new DomainException(OrderMessage.OrderError.CART_ID_REQUIRED);
        }

        var json = await _cartRedisService.GetRawCartAsync(cartId);
        if (string.IsNullOrEmpty(json))
        {
            throw new DomainException(OrderMessage.OrderError.CART_NOT_FOUND_OR_EXPIRED);
        }

        var cart = JsonSerializer.Deserialize<CartModel>(json)
                   ?? throw new DomainException(OrderMessage.OrderError.INVALID_CART_DATA);

        cart = await SyncCartPricingAndAvailabilityAsync(cart);

        return _mapper.Map<CartDto>(cart);
    }

    public async Task<CartDto> UpdateCartItemQuantityAsync(UpdateCartItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CartId))
            throw new DomainException(OrderMessage.OrderError.CART_ID_REQUIRED);

        if (request.NewQuantity < 0)
            throw new DomainException(OrderMessage.OrderError.QUANTITY_MUST_BE_GREATER_THAN_ZERO);

        var json = await _cartRedisService.GetRawCartAsync(request.CartId);
        if (string.IsNullOrEmpty(json))
            throw new DomainException(OrderMessage.OrderError.CART_NOT_FOUND_OR_EXPIRED);

        var cart = JsonSerializer.Deserialize<CartModel>(json)
                   ?? throw new DomainException(OrderMessage.OrderError.INVALID_CART_DATA);

        var existingItem = cart.Items.FirstOrDefault(i => i.DishId == request.DishId);
        if (existingItem == null)
            throw new DomainException(OrderMessage.OrderError.ITEM_NOT_FOUND_IN_CART);

        // Nếu NewQuantity = 0 thì xóa món khỏi giỏ hàng
        if (request.NewQuantity == 0)
        {
            cart.Items.Remove(existingItem);
        }
        else
        {
            // Kiểm tra số lượng tồn kho thực tế
            var branchDish = await _unitOfWork.BranchDishConfigs.FirstOrDefaultAsync(
                b => b.RestaurantId == cart.RestaurantId && b.DishId == request.DishId);

            if (branchDish == null)
                throw new DomainException(DishMessage.DishError.DISH_NOT_FOUND);

            if (!branchDish.IsSelling)
                throw new DomainException(BranchDishMessage.BranchDishError.NOT_SELLING);

            if (branchDish.IsSoldOut)
                throw new DomainException(BranchDishMessage.BranchDishError.SOLD_OUT);

            // Nếu số lượng yêu cầu vượt quá tồn kho thì báo lỗi
            if (branchDish.DishAvailability > 0 && request.NewQuantity > branchDish.DishAvailability)
                throw new DomainException(
                    string.Format(OrderMessage.OrderError.QUANTITY_EXCEEDS_AVAILABLE_STOCK, branchDish.DishAvailability));

            existingItem.Quantity = request.NewQuantity;
            existingItem.SubTotal = existingItem.DiscountedPrice * existingItem.Quantity;
        }

        cart.TotalAmount = cart.Items.Sum(i => i.SubTotal);

        var updatedJson = JsonSerializer.Serialize(cart);
        await _cartRedisService.SaveRawCartAsync(request.CartId, updatedJson, TimeSpan.FromMinutes(60));

        cart = await SyncCartPricingAndAvailabilityAsync(cart);

        return _mapper.Map<CartDto>(cart);
    }

    private async Task<CartModel> SyncCartPricingAndAvailabilityAsync(CartModel cart)
    {
        if (cart.Items == null || !cart.Items.Any())
            return cart;

        var dishIds = cart.Items.Select(i => i.DishId).ToList();
        var dishesWithPromo = await GetDishesByIdsWithPromotionAsync(cart.RestaurantId, dishIds);

        bool isUpdated = false;
        var itemsToRemove = new List<CartItemModel>();

        foreach (var item in cart.Items)
        {
            var dishInfo = dishesWithPromo.FirstOrDefault(d => d.DishId == item.DishId);

            if (dishInfo == null || dishInfo.IsSoldOut)
            {
                itemsToRemove.Add(item);
                isUpdated = true;
                continue;
            }

            if (item.DiscountedPrice != dishInfo.DiscountedPrice)
            {
                if (item.OriginalPrice == 0)
                {
                    item.OriginalPrice = dishInfo.Price;
                }

                item.DiscountedPrice = dishInfo.DiscountedPrice;
                item.PromotionAmount = (item.OriginalPrice - item.DiscountedPrice) * item.Quantity;
                item.PromotionName = dishInfo.PromotionName;
                item.SubTotal = item.DiscountedPrice * item.Quantity;
                isUpdated = true;
            }

            if (item.Quantity > dishInfo.DishAvailabilityStock)
            {
                item.Quantity = Math.Max(0, dishInfo.DishAvailabilityStock);
                if (item.Quantity == 0)
                {
                    itemsToRemove.Add(item);
                }
                else
                {
                    item.SubTotal = item.DiscountedPrice * item.Quantity;
                }

                isUpdated = true;
            }
        }

        if (itemsToRemove.Any())
        {
            foreach (var item in itemsToRemove) cart.Items.Remove(item);
        }

        if (isUpdated)
        {
            cart.TotalAmount = cart.Items.Sum(i => i.SubTotal);
            var updatedJson = JsonSerializer.Serialize(cart);
            await _cartRedisService.SaveRawCartAsync(cart.CartId, updatedJson, TimeSpan.FromMinutes(60));
        }

        return cart;
    }

    public async Task<PaymentQrDto> GetPaymentQrAsync(string cartId, string phone, bool isPreOrder, DateTime? requestedPickupAt, int? appliedPromotionId)
    {
        if (string.IsNullOrWhiteSpace(cartId))
            throw new DomainException(OrderMessage.OrderError.CART_ID_REQUIRED);

        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException(OrderMessage.OrderError.PHONE_REQUIRED);

        if (isPreOrder && requestedPickupAt == null)
            throw new DomainException("RequestedPickupAt là bắt buộc cho đơn đặt trước.");

        var json = await _cartRedisService.GetRawCartAsync(cartId);
        if (string.IsNullOrEmpty(json))
            throw new DomainException(OrderMessage.OrderError.CART_NOT_FOUND_OR_EXPIRED);

        var cart = JsonSerializer.Deserialize<CartModel>(json)
                   ?? throw new DomainException(OrderMessage.OrderError.INVALID_CART_DATA);

        if (cart.Items == null || !cart.Items.Any())
            throw new DomainException(OrderMessage.OrderError.CART_EMPTY_CANNOT_CREATE_PAYMENT);

        var restaurant = await _unitOfWork.Restaurants.GetByIdWithTenantBankAsync(cart.RestaurantId);
        if (restaurant?.Tenant == null)
            throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

        var tenant = restaurant.Tenant;
        if (tenant.BankId == null || tenant.Bank == null || string.IsNullOrWhiteSpace(tenant.CardNumber))
            throw new DomainException(OrderMessage.OrderError.RESTAURANT_NO_BANK_CONFIGURED);

        if (!tenant.IsVerifyBank)
            throw new DomainException(OrderMessage.OrderError.RESTAURANT_BANK_NOT_VERIFIED);

        var activeShift = await _unitOfWork.Shifts.GetActiveCashierShiftAsync(cart.RestaurantId);
        if (activeShift == null)
            throw new DomainException(ShiftMessage.ShiftError.SHIFT_NOT_OPEN_YET);

        
        // Treat null as "not explicitly disabled" to avoid null-casts.
        if (restaurant.IsActive == false)
            throw new DomainException("Nhà hàng hiện không hoạt động.");

        if (restaurant.IsReceivingOrders == false)
            throw new DomainException("Nhà hàng hiện không nhận đơn.");

        decimal promotionDiscount = 0;
        if (appliedPromotionId.HasValue)
        {
            var promotion = await _unitOfWork.Promotions.GetByFieldsIncludeAsync(p => p.Id == appliedPromotionId.Value, p => p.RestaurantPromotions);
            if (promotion == null || promotion.IsDeleted || !promotion.IsActive || promotion.Scope != PromotionScope.Order)
                throw new DomainException("Mã khuyến mãi không hợp lệ.");

            if (!promotion.IsValidAt(TimeUtils.GetVietnamTimeNow()))
                throw new DomainException("Mã khuyến mãi đã hết hạn hoặc chưa tới khung giờ áp dụng.");

            if (cart.TotalAmount < promotion.MinOrderValue)
                throw new DomainException($"Đơn hàng chưa đạt giá trị tối thiểu {promotion.MinOrderValue} để áp dụng mã.");

            if (!promotion.IsGlobal)
            {
                var isForRestaurant = promotion.RestaurantPromotions.Any(rp => rp.RestaurantId == cart.RestaurantId);
                if (!isForRestaurant)
                    throw new DomainException("Mã khuyến mãi không áp dụng cho nhà hàng này.");
            }

            promotionDiscount = CalculateDiscountValue(cart.TotalAmount, promotion);
        }

        var finalAmount = (decimal)PricingUtils.RoundToNearestThousand(Math.Max(0, cart.TotalAmount - promotionDiscount));
        var amount = finalAmount;

        // sinh mã qr
        Guid orderId = Guid.NewGuid();
        string qrContent = orderId.ToString();
        var qrBytes = _qrCodeService.GenerateQrCodeBytes(qrContent);
        string qrBase64DataUri = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";
        string qrOrderUrl = _storageService.GetOrderQrUrl(orderId);

        var (qrUrl, paymentCode) = BankQrLinkUtils.GenerateSePayQrUrl(
            tenant.CardNumber,
            tenant.Bank.ShortName,
            amount,
            PaymentIntent.OrderPayment);

        try
        {
            // bắt đầu giao dịch với db
            await using var tx = await _unitOfWork.BeginTransactionAsync();

            var dishQuantities = cart.Items.ToDictionary(i => i.DishId, i => i.Quantity);
            var failedDishIds = await _unitOfWork.BranchDishConfigs.ReserveDishAvailabilityBatchAsync(cart.RestaurantId, dishQuantities);

            if (failedDishIds.Any())
            {
                var failedId = failedDishIds.First();
                var failedName = cart.Items.First(i => i.DishId == failedId).DishName;
                var ex = new DomainException(string.Format(OrderMessage.OrderError.DISH_OUT_OF_STOCK, failedName));
                ex.Data["failedDishId"] = failedId;
                throw ex;
            }

            int orderCode = 0;
            var (startUtc, endUtc, dateInt) = TimeUtils.GetVietnamDayRangeUtc();
            orderCode = await _unitOfWork.Orders.GetNextDailyOrderCodeAsync(
                cart.RestaurantId, startUtc, endUtc, dateInt);

            var order = new Order
            {
                Id = orderId,
                RestaurantId = cart.RestaurantId,
                PromotionId = appliedPromotionId,
                OrderCode = orderCode,
                IsPreOrder = isPreOrder,
                RequestedPickupAt = isPreOrder ? requestedPickupAt : null,
                Note = cart.Note,
                TotalAmount = cart.TotalAmount,
                PromotionDiscount = promotionDiscount,
                FinalAmount = finalAmount,
                Status = OrderStatus.Unpaid,
                IsScanned = false,
                Type = "SePay",
                NumberPhone = phone,
                QrCodeUrl = qrOrderUrl
            };

            await _unitOfWork.Orders.AddAsync(order);

            var details = cart.Items.Select(i => new OrderDetail
            {
                OrderId = orderId,
                DishId = i.DishId,
                Quantity = i.Quantity,
                DiscountedPrice = i.DiscountedPrice,
                OriginalPrice = i.OriginalPrice,
                PromotionAmount = i.PromotionAmount,
                SubTotal = i.SubTotal
            }).ToList();

            await _unitOfWork.OrderDetails.AddRangeAsync(details);

            // gộp tạo giao dịch thanh toán vào chung transaction để đảm bảo toàn vẹn dữ liệu
            await _unitOfWork.Transactions.AddAsync(new Transaction
            {
                OrderId = orderId,
                Status = OrderTransactionStatus.Pending,
                TotalAmount = amount,
                TransactionCode = paymentCode,
                PaymentMethod = PaymentMethod.BankTransfer,
                ShiftId = activeShift.Id,
                TransactionType = TransactionType.Payment
            });

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();

            // Không chặn response nếu enqueue job (Hangfire/DB) bị chậm.
            _ = Task.Run(() =>
            {
                try
                {
                    _backgroundJobService.EnqueueUploadOrderQr(qrBytes, orderId);
                    if (orderCode > 0)
                    {
                        _backgroundJobService.EnqueueGeneratePaymentAudio(orderCode, amount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Enqueue background jobs failed for order {OrderId}", orderId);
                }
            });

            try
            {
                var reservedTuples = dishQuantities.Select(r => (r.Key, r.Value));
                var orderRealtime = new OrderRealtimeDto
                {
                    Id = order.Id,
                    OrderCode = order.OrderCode,
                    Phone = order.NumberPhone,
                    TotalAmount = order.FinalAmount,
                    Note = order.Note,
                    Status = (int)order.Status,
                    Items = cart.Items.Select(i => new OrderItemRealtimeDto
                    {
                        DishId = i.DishId,
                        Quantity = i.Quantity,
                        Price = i.DiscountedPrice
                    }).ToList()
                };

                _ = Task.WhenAll(
                        _menuCacheService.UpdateMenuStockInCacheAsync(cart.RestaurantId, reservedTuples),
                        _realtimeService.SendOrderToKitchen(order.RestaurantId.ToString(), orderRealtime)
                    )
                    .ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            _logger.LogError(t.Exception,
                                "Async post-checkout tasks failed for order {OrderId}",
                                order.Id);
                        }
                    }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send realtime update or invalidate cache for order {OrderId}", order.Id);
            }

            return new PaymentQrDto
            {
                OrderId = orderId,
                QrUrl = qrUrl,
                PaymentCode = paymentCode,
                TotalAmount = amount,
                RestaurantName = restaurant.RestaurantName ?? "",
                Phone = phone,
                QrCodeBase64 = qrBase64DataUri
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình GetPaymentQrAsync cho cart {CartId}", cartId);
            throw;
        }
    }

    public async Task<CashCheckoutResponse> CheckoutCashAsync(CashCheckoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CartId))
            throw new DomainException(OrderMessage.OrderError.CART_ID_REQUIRED);

        if (string.IsNullOrWhiteSpace(request.Phone))
            throw new DomainException(OrderMessage.OrderError.PHONE_REQUIRED);

        var json = await _cartRedisService.GetRawCartAsync(request.CartId);
        if (string.IsNullOrEmpty(json))
            throw new DomainException(OrderMessage.OrderError.CART_NOT_FOUND_OR_EXPIRED);

        var cart = JsonSerializer.Deserialize<CartModel>(json)
                   ?? throw new DomainException(OrderMessage.OrderError.INVALID_CART_DATA);

        if (cart.Items == null || !cart.Items.Any())
            throw new DomainException(OrderMessage.OrderError.CART_EMPTY_CANNOT_CREATE_ORDER);

        var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(cart.RestaurantId);
        if (restaurant == null)
            throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

        // Validate trạng thái hoạt động của nhà hàng
        if (restaurant.IsActive == false)
            throw new DomainException("Nhà hàng hiện không hoạt động.");

        if (restaurant.IsReceivingOrders == false)
            throw new DomainException("Nhà hàng hiện không nhận đơn.");

        var activeShift = await _unitOfWork.Shifts.GetActiveCashierShiftAsync(cart.RestaurantId);

        if (activeShift == null)
            throw new DomainException(ShiftMessage.ShiftError.SHIFT_NOT_OPEN_YET);

        decimal promotionDiscount = 0;
        if (request.AppliedPromotionId.HasValue)
        {
            var promotion = await _unitOfWork.Promotions.GetByFieldsIncludeAsync(p => p.Id == request.AppliedPromotionId.Value, p => p.RestaurantPromotions);
            if (promotion == null || promotion.IsDeleted || !promotion.IsActive || promotion.Scope != PromotionScope.Order)
                throw new DomainException("Mã khuyến mãi không hợp lệ.");

            if (!promotion.IsValidAt(TimeUtils.GetVietnamTimeNow()))
                throw new DomainException("Mã khuyến mãi đã hết hạn hoặc chưa tới khung giờ áp dụng.");

            if (cart.TotalAmount < promotion.MinOrderValue)
                throw new DomainException($"Đơn hàng chưa đạt giá trị tối thiểu {promotion.MinOrderValue} để áp dụng mã.");

            if (!promotion.IsGlobal)
            {
                var isForRestaurant = promotion.RestaurantPromotions.Any(rp => rp.RestaurantId == cart.RestaurantId);
                if (!isForRestaurant)
                    throw new DomainException("Mã khuyến mãi không áp dụng cho nhà hàng này.");
            }

            promotionDiscount = CalculateDiscountValue(cart.TotalAmount, promotion);
        }

        var finalAmount = (decimal)PricingUtils.RoundToNearestThousand(Math.Max(0, cart.TotalAmount - promotionDiscount));
        var amount = finalAmount;

        // sinh mã qr
        Guid orderId = Guid.NewGuid();
        string qrContent = orderId.ToString();
        var qrBytes = _qrCodeService.GenerateQrCodeBytes(qrContent);
        string qrBase64DataUri = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";
        string qrOrderUrl = _storageService.GetOrderQrUrl(orderId);

        try
        {
            // bắt đầu giao dịch với db
            await using var tx = await _unitOfWork.BeginTransactionAsync();

            var dishQuantities = cart.Items.ToDictionary(i => i.DishId, i => i.Quantity);
            var failedDishIds = await _unitOfWork.BranchDishConfigs.ReserveDishAvailabilityBatchAsync(cart.RestaurantId, dishQuantities);

            if (failedDishIds.Any())
            {
                var failedId = failedDishIds.First();
                var failedName = cart.Items.First(i => i.DishId == failedId).DishName;
                var ex = new DomainException(string.Format(OrderMessage.OrderError.DISH_OUT_OF_STOCK, failedName));
                ex.Data["failedDishId"] = failedId;
                throw ex;
            }
            int orderCode;

            var (startUtc, endUtc, dateInt) = TimeUtils.GetVietnamDayRangeUtc();
            orderCode = await _unitOfWork.Orders.GetNextDailyOrderCodeAsync(
                cart.RestaurantId, startUtc, endUtc, dateInt);

            var order = new Order
            {
                Id = orderId,
                RestaurantId = cart.RestaurantId,
                PromotionId = request.AppliedPromotionId,
                OrderCode = orderCode,
                IsPreOrder = false,
                Note = cart.Note,
                TotalAmount = cart.TotalAmount,
                PromotionDiscount = promotionDiscount,
                FinalAmount = finalAmount,
                Status = OrderStatus.Unpaid,
                IsScanned = false,
                Type = "Cash",
                NumberPhone = request.Phone,
                QrCodeUrl = qrOrderUrl
            };

            await _unitOfWork.Orders.AddAsync(order);

            var details = cart.Items.Select(i => new OrderDetail
            {
                OrderId = orderId,
                DishId = i.DishId,
                Quantity = i.Quantity,
                DiscountedPrice = i.DiscountedPrice,
                OriginalPrice = i.OriginalPrice,
                PromotionAmount = i.PromotionAmount,
                SubTotal = i.SubTotal
            }).ToList();

            await _unitOfWork.OrderDetails.AddRangeAsync(details);

            await _unitOfWork.Transactions.AddAsync(new Transaction
            {
                OrderId = orderId,
                Status = OrderTransactionStatus.Pending,
                TotalAmount = amount,
                TransactionCode = null,
                PaymentMethod = PaymentMethod.Cash,
                ShiftId = activeShift.Id,
                TransactionType = TransactionType.Payment
            });
         
            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();

            // Không chặn response nếu enqueue job (Hangfire/DB) bị chậm.
            _ = Task.Run(() =>
            {
                try
                {
                    _backgroundJobService.EnqueueUploadOrderQr(qrBytes, orderId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Enqueue UploadOrderQr failed for cash order {OrderId}", orderId);
                }
            });
           
            try
            {
                var reservedTuples = dishQuantities.Select(r => (r.Key, r.Value));
                var orderRealtime = new OrderRealtimeDto
                {
                    Id = order.Id,
                    OrderCode = order.OrderCode,
                    Phone = order.NumberPhone,
                    TotalAmount = order.FinalAmount,
                    Note = order.Note,
                    Status = (int)order.Status,
                    Items = cart.Items.Select(i => new OrderItemRealtimeDto
                    {
                        DishId = i.DishId,
                        Quantity = i.Quantity,
                        Price = i.DiscountedPrice
                    }).ToList()
                };

                // Critical: xóa cart để tránh checkout lặp
                await _cartRedisService.DeleteCartAsync(request.CartId);

                // Non-critical: không chặn trả kết quả nếu SignalR/cache chậm
                _ = Task.WhenAll(
                        _menuCacheService.UpdateMenuStockInCacheAsync(cart.RestaurantId, reservedTuples),
                        _realtimeService.SendOrderToKitchen(order.RestaurantId.ToString(), orderRealtime)
                    )
                    .ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            _logger.LogError(t.Exception,
                                "Async post-checkout (cash) tasks failed for order {OrderId}",
                                order.Id);
                        }
                    }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi realtime hoặc invalidate cache cho đơn {OrderId}", order.Id);
            }


            return new CashCheckoutResponse
            {
                OrderId = orderId,
                OrderCode = orderCode,
                TotalAmount = amount,
                RestaurantName = restaurant.RestaurantName,
                Phone = request.Phone,
                Note = cart.Note,
                Status = OrderStatus.Unpaid,
                QrCodeBase64 = qrBase64DataUri
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task ConfirmCashPaymentAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
            throw new DomainException(OrderMessage.OrderError.INVALID_ORDER_ID);

        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            throw new DomainException(OrderMessage.OrderError.ORDER_NOT_FOUND);

        if (order.Status != OrderStatus.Unpaid)
        {           
            return;
        }
       
        if (_authenticatedUserService.ProfileId == null)
            throw new DomainException(OrderMessage.OrderError.STAFF_NOT_IDENTIFIED);


        var staff = await _unitOfWork.Staffs.GetByIdAsync(_authenticatedUserService.ProfileId.Value);
        if (staff == null)
            throw new DomainException(StaffMessage.StaffError.STAFF_NOT_FOUND);
        if (staff.RestaurantId != order.RestaurantId)
            throw new DomainException(StaffMessage.StaffError.STAFF_NOT_IN_RESTAURANT);

        var transaction = await _unitOfWork.Transactions.FirstOrDefaultAsync(
            t => t.OrderId == orderId && t.PaymentMethod == PaymentMethod.Cash);

        if (transaction == null)
            throw new DomainException(OrderMessage.OrderError.CASH_TRANSACTION_NOT_FOUND);

        if (transaction.Status == OrderTransactionStatus.Success)
        {
            return;
        }

        var activeShift = await _unitOfWork.Shifts.GetActiveCashierShiftAsync(order.RestaurantId);

        if (activeShift == null)
            throw new DomainException(ShiftMessage.ShiftError.SHIFT_NOT_OPEN_YET);

        transaction.ShiftId = activeShift.Id;

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        try
        {
            order.Status = OrderStatus.Pending; 
            _unitOfWork.Orders.Update(order);

            transaction.Status = OrderTransactionStatus.Success;
            _unitOfWork.Transactions.Update(transaction);

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();
            string audioUrl = string.Empty;
            try
            {
                if (_realtimeService != null)
                {
                    await _realtimeService.NotifyOrderStatusChanged(
                        order.RestaurantId.ToString(),
                        order.Id.ToString(),
                        (int)order.Status
                    );

                    await _realtimeService.NotifyCustomerOrderStatusChanged(order.Id.ToString(), (int)order.Status);

                    await _realtimeService.NotifyPaymentReceived(
                        order.RestaurantId.ToString(),
                        order.OrderCode,
                        transaction.TotalAmount,
                        audioUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Lỗi SignalR khi xác nhận thanh toán tiền mặt. OrderId={OrderId}",
                    order.Id);
            }
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

    }

    public async Task<List<CashPendingOrderResponse>> GetCashOrdersPendingConfirmAsync()
    {
        if (_authenticatedUserService.ProfileId == null)
            throw new DomainException(OrderMessage.OrderError.STAFF_NOT_IDENTIFIED);

        var staff = await _unitOfWork.Staffs.GetByIdAsync(_authenticatedUserService.ProfileId.Value)
            ?? throw new DomainException(StaffMessage.StaffError.STAFF_NOT_FOUND);

        var orders = await _unitOfWork.Orders.GetCashOrdersPendingConfirmAsync(staff.RestaurantId);

        if (orders == null)
            return new List<CashPendingOrderResponse>();

        return orders.Select(o => new CashPendingOrderResponse
        {
            Id = o.Id.ToString(),
            OrderCode = o.OrderCode,
            CreatedAt = o.CreatedAt,
            Amount = o.TotalAmount,
            FinalAmount = o.FinalAmount,
            PromotionDiscount = o.PromotionDiscount,
            PromotionName = o.Promotion?.Name,
            Phone = o.NumberPhone,
            Note = o.Note,
            Type = o.Type,
            Items = o.OrderDetails.Select(od => new CashPendingOrderItem
            {
                DishName = od.Dish?.DishName,
                Quantity = od.Quantity,
                OriginalPrice = od.OriginalPrice,
                DiscountedPrice = od.DiscountedPrice,
                PromotionAmount = od.PromotionAmount,
                SubTotal = od.SubTotal
            }).ToList()
        }).ToList();
    }

    public async Task EnsureOrderInStaffRestaurantAsync(int orderNumber)
    {
        if (_authenticatedUserService.ProfileId == null)
            throw new DomainException(OrderMessage.OrderError.STAFF_NOT_IDENTIFIED);

        var staff = await _unitOfWork.Staffs.GetByIdAsync(_authenticatedUserService.ProfileId.Value);
        if (staff == null)
            throw new DomainException(StaffMessage.StaffError.STAFF_NOT_FOUND);

        var order = await _unitOfWork.Orders.GetByOrderCodeAndRestaurantAsync(orderNumber, staff.RestaurantId);
        if (order == null)
            throw new DomainException(OrderMessage.OrderError.ORDER_SEQUENCE_NOT_FOUND_IN_RESTAURANT);
    }

    public async Task ProcessOrderPaymentAsync(string paymentCode, decimal transferAmount)
    {
        if (string.IsNullOrWhiteSpace(paymentCode))
            throw new DomainException(OrderMessage.OrderError.PAYMENT_CODE_REQUIRED);

        if (transferAmount <= 0)
            throw new DomainException(OrderMessage.OrderError.INVALID_PAYMENT_AMOUNT);

        var transaction = await _unitOfWork.Transactions.FirstOrDefaultAsync(
            t => t.TransactionCode == paymentCode);
        if (transaction == null)
            throw new DomainException(OrderMessage.OrderError.TRANSACTION_NOT_FOUND);

        if (transaction.Status == OrderTransactionStatus.Success)
        {
            return;
        }

        var orderId = transaction.OrderId;
        if (orderId == Guid.Empty)
            throw new DomainException(OrderMessage.OrderError.INVALID_ORDER_ID);

        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            throw new DomainException(OrderMessage.OrderError.ORDER_NOT_FOUND);

        if (order.Status != OrderStatus.Unpaid)
        {
            return;
        }

        var expectedAmount = Math.Round(order.FinalAmount);
        if (Math.Round(transferAmount) < expectedAmount)
            throw new DomainException(OrderMessage.OrderError.PAYMENT_AMOUNT_MISMATCH);

        // Prefer ShiftId saved during checkout. Only fallback to active shift if ShiftId is missing.
        if (!transaction.ShiftId.HasValue)
        {
            var activeShift = await _unitOfWork.Shifts.FirstOrDefaultAsync(
                s => s.RestaurantId == order.RestaurantId && s.Status == ShiftStatus.Open);

            if (activeShift != null)
            {
                transaction.ShiftId = activeShift.Id;
            }
            else
            {
                _logger.LogWarning(
                    "ProcessOrderPaymentAsync: PaymentCode={PaymentCode} has no ShiftId and no active shift. RestaurantId={RestaurantId}",
                    paymentCode,
                    order.RestaurantId);
            }
        }

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        try
        {
            order.Status = OrderStatus.Pending;
            _unitOfWork.Orders.Update(order);
           
            transaction.Status = OrderTransactionStatus.Success;
            _unitOfWork.Transactions.Update(transaction);

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();
            try
            {
                if (_realtimeService != null)
                {
                    await _realtimeService.NotifyOrderStatusChanged(
                        order.RestaurantId.ToString(),
                        order.Id.ToString(),
                        (int)order.Status
                    );

                    await _realtimeService.NotifyCustomerOrderStatusChanged(order.Id.ToString(), (int)order.Status);

                    var audioUrl = string.Empty;
                    try
                    {
                        audioUrl = await _storageService.GetOrGeneratePaymentReceivedAudioAsync(order.OrderCode, transferAmount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Tạo audio thông báo đã nhận chuyển khoản thất bại. OrderCode={OrderCode}, Amount={Amount}",
                            order.OrderCode,
                            transferAmount);
                    }

                    await _realtimeService.NotifyPaymentReceived(order.RestaurantId.ToString(), order.OrderCode, transferAmount, audioUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Lỗi SignalR khi xử lý SePay webhook. PaymentCode={PaymentCode}, OrderId={OrderId}",
                    paymentCode,
                    order.Id);
            }
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<KdsOrderResponse>> GetKdsActiveOrders(int restaurantId)
    {
        var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);
        if (restaurant == null)
            throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);


        var orders = await _unitOfWork.Orders.GetOrdersForKdsAsync(restaurantId);

        if (orders == null || !orders.Any()) return new List<KdsOrderResponse>();

        var refundOrderIds = orders
            .Where(o => o.RefundOrderId.HasValue)
            .Select(o => o.RefundOrderId!.Value)
            .Distinct()
            .ToList();

        var originalOrderCodes = new Dictionary<Guid, int>();
        if (refundOrderIds.Any())
        {
            var originalOrders = await _unitOfWork.Orders.FindAsync(o => refundOrderIds.Contains(o.Id));
            originalOrderCodes = originalOrders.ToDictionary(o => o.Id, o => o.OrderCode);
        }

        return orders.Select(order => new KdsOrderResponse
        {
            Id = order.Id.ToString(),
            OrderCode = order.OrderCode,
            CreatedAt = order.CreatedAt,
            RequestedPickupAt = order.RequestedPickupAt,
            ConfirmedPickupAt = order.ConfirmedPickupAt,
            Amount = order.FinalAmount,
            TotalAmount = order.TotalAmount,
            FinalAmount = order.FinalAmount,
            PromotionDiscount = order.PromotionDiscount,
            PromotionName = order.Promotion?.Name,
            Phone = order.NumberPhone,
            Status = (int)order.Status,
            IsPreOrder = order.IsPreOrder,
            Type = order.Type,
            Note = order.Note,
            PaymentProofUrl = order.PaymentProofUrl,
            TypeOrder = (int)order.typeOrder,
            RefundType = order.RefundType.HasValue ? (int)order.RefundType.Value : null,
            OriginalOrderCode = order.RefundOrderId.HasValue && originalOrderCodes.ContainsKey(order.RefundOrderId.Value)
                ? originalOrderCodes[order.RefundOrderId.Value]
                : null,

            Items = order.OrderDetails.Select(od => new KdsItemResponse
            {
                Id = od.Id.ToString(),
                Name = od.Dish.DishName,
                OriginalPrice = od.OriginalPrice,
                DiscountedPrice = od.DiscountedPrice,
                PromotionAmount = od.PromotionAmount,
                Quantity = od.Quantity,
                RefundedQuantity = od.RefundedQuantity,
                Image = od.Dish.ImageUrl
            }).ToList()
        }).ToList();
    }
    
    public async Task<bool> UpdateOrderStatus(Guid orderId, OrderStatus newStatus)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null) throw new DomainException(OrderMessage.OrderError.ORDER_NOT_FOUND);

        if (newStatus != OrderStatus.Cancelled && (int)newStatus <= (int)order.Status)
        {
            throw new DomainException($"Không thể cập nhật trạng thái từ {order.Status} sang {newStatus}. Trạng thái chỉ có thể được cập nhật tiến trình.");
        }

        if (order.IsPreOrder && order.Status == OrderStatus.Pending && 
            newStatus >= OrderStatus.Preparing && order.ConfirmedPickupAt == null)
        {
            throw new DomainException("Đơn hàng đặt trước cần được xác nhận thời gian nhận hàng trước khi chế biến.");
        }

        if (order.Status == OrderStatus.Served)
        {
            throw new DomainException("Đơn hàng đã hoàn thành (Served), không thể cập nhật thêm.");
        }

        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Orders.Update(order);
        await _unitOfWork.SaveAsync();

        if (_realtimeService != null)
        {
            await _realtimeService.NotifyOrderStatusChanged(
                order.RestaurantId.ToString(),
                order.Id.ToString(),
                (int)newStatus
            );
            await _realtimeService.NotifyCustomerOrderStatusChanged(order.Id.ToString(), (int)newStatus);
        }
        return true;
    }

    public async Task<List<CustomerOrderSummaryDto>> GetCustomerActiveOrdersAsync(int restaurantId, string phone)
    {
        if (restaurantId <= 0)
            throw new DomainException("RestaurantId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("Số điện thoại không được để trống.");

        phone = phone.Trim();

        var orders = await _unitOfWork.Orders.GetCustomerActiveOrdersAsync(restaurantId, phone);

        var dtos = _mapper.Map<List<CustomerOrderSummaryDto>>(orders);
        CustomerOrderSummaryAmounts.ApplyOriginalAndFinalFromEntities(orders, dtos);
        return dtos;
    }

    public async Task<List<CustomerOrderSummaryDto>> GetCustomerActiveOrdersAllRestaurantsAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("Số điện thoại không được để trống.");

        phone = phone.Trim();

        var orders = await _unitOfWork.Orders.GetCustomerActiveOrdersAllRestaurantsAsync(phone);

        var dtos = _mapper.Map<List<CustomerOrderSummaryDto>>(orders);
        CustomerOrderSummaryAmounts.ApplyOriginalAndFinalFromEntities(orders, dtos);
        return dtos;
    }

    public async Task<List<MenuDishItemDto>> GetDishesByIdsWithPromotionAsync(int restaurantId, List<int> dishIds)
    {
        if (dishIds == null || !dishIds.Any())
            throw new DomainException(OrderMessage.OrderError.DISH_ID_LIST_REQUIRED);

        var now = TimeUtils.GetVietnamTimeNow();

        var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId)
                         ?? throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

        var tenantId = restaurant.TenantId;

        var features = await _planLimitationService.GetRestaurantFeaturesAsync(restaurantId);

        var basePromotions = features.CanUsePromotions ? await _unitOfWork.Promotions.GetAllAsync(
            predicate: p =>
                p.TenantId == tenantId &&
                p.IsActive &&
                !p.IsDeleted &&
                p.Scope == PromotionScope.Dish &&
                (p.IsGlobal || (p.RestaurantPromotions.Any(rp => rp.RestaurantId == restaurantId)
                                && !p.PromotionDishes.Any())),
            p => p.RestaurantPromotions,
            p => p.PromotionDishes
        ) : new List<Promotion>();

        var branchDishes = await _unitOfWork.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(restaurantId, dishIds);

        var result = branchDishes.Select(bdc =>
        {
            var specificDishPromos = features.CanUsePromotions ? (bdc.Dish.PromotionDishes?
                                         .Select(pd => pd.Promotion)
                                         .Where(p => p.Scope == PromotionScope.Dish &&
                                                     p.IsActive &&
                                                     !p.IsDeleted)
                                     ?? Enumerable.Empty<Promotion>())
                                     : Enumerable.Empty<Promotion>();

            var allEligiblePromotions = basePromotions.Concat(specificDishPromos);

            var winningPromo = allEligiblePromotions
                .Where(p => p.IsValidAt(now) && (bdc.Price - CalculateDiscountValue(bdc.Price, p) > 1000))
                .OrderByDescending(p => p.Priority)
                    .ThenByDescending(p => CalculateDiscountValue(bdc.Price, p))
                .FirstOrDefault();

            int discountedPrice = (int)bdc.Price;
            string? promoLabel = null;

            if (winningPromo != null)
            {
                var discountAmount = CalculateDiscountValue(bdc.Price, winningPromo);
                discountedPrice = (int)Math.Max(bdc.Price - discountAmount, 0);

                promoLabel = winningPromo.DiscountType == DiscountType.Percentage
                    ? $"-{winningPromo.DiscountValue}%"
                    : $"-{(PricingUtils.RoundToNearestThousand(winningPromo.DiscountValue) / 1000):G}k";
            }

            return new MenuDishItemDto
            {
                DishId = bdc.DishId,
                DishName = bdc.Dish.DishName,
                Description = bdc.Dish.Description,
                ImageUrl = bdc.Dish.ImageUrl,
                Price = (int)bdc.Price,
                DiscountedPrice = discountedPrice,
                PromotionName = winningPromo?.Name,
                PromotionLabel = promoLabel,
                PromoType = winningPromo?.Type,
                Type = bdc.Dish.Type,
                DishAvailabilityStock = bdc.DishAvailability,
                ExpiredAt = winningPromo != null ? CalculateTrueExpiredAt(winningPromo, now) : null,
                IsSoldOut = bdc.IsSoldOut,
                ComboItems = bdc.Dish.Type == DishType.Combo
                    ? bdc.Dish.ComboDetails.Select(cd => new ComboItemDto
                    {
                        DishId = cd.ItemDishId,
                        DishName = cd.ItemDish.DishName,
                        ImageUrl = cd.ItemDish.ImageUrl,
                        Quantity = cd.Quantity
                    }).ToList()
                    : new List<ComboItemDto>()
            };
        }).ToList();

        return result;
    }

    public async Task<string> ValidateQrCodeAsync(string qrContent, int orderNumber)
    {
        if (string.IsNullOrWhiteSpace(qrContent))
            throw new DomainException(OrderMessage.OrderError.QR_INVALID);

        if (!Guid.TryParse(qrContent, out var orderId))
            throw new DomainException(OrderMessage.OrderError.QR_ORDER_ID_INVALID);

        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);

        if (order == null)
            throw new DomainException(OrderMessage.OrderError.ORDER_NOT_FOUND);

        if (order.Status == OrderStatus.Served)
            throw new DomainException(OrderMessage.OrderError.QR_ALREADY_SCANNED);

        if (order.Status != OrderStatus.Ready)
            throw new DomainException(OrderMessage.OrderError.ORDER_NOT_READY);

        order.Status = OrderStatus.Served;

        await _unitOfWork.SaveAsync();

        try
        {
            if (_realtimeService != null)
            {
                await _realtimeService.NotifyOrderStatusChanged(
                    order.RestaurantId.ToString(),
                    order.Id.ToString(),
                    (int)order.Status
                );

                await _realtimeService.NotifyCustomerOrderStatusChanged(order.Id.ToString(), (int)order.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi SignalR khi validate scan QR. OrderId={OrderId}", order.Id);
        }

        string textInput =
            $"Đã xác nhận thành công đơn hàng {orderNumber}";

        string audioUrl = await _storageService.GetOrGenerateScanAudioAsync(orderNumber, textInput);

        return audioUrl;
    }

    // Calculate discount value based on promotion type and rules
    private static decimal CalculateDiscountValue(decimal price, Promotion p)
    {
        if (p.DiscountType == DiscountType.FixedAmount)
            return p.DiscountValue;

        var discount = price * (p.DiscountValue / 100);

        return p.MaxDiscountValue.HasValue
            ? Math.Min(discount, p.MaxDiscountValue.Value)
            : discount;
    }
    // Calculate the actual expiration time of a promotion considering its type and daily time rules
    private static DateTime? CalculateTrueExpiredAt(Promotion p, DateTime now)
    {
        var today = now.Date;
        DateTime? trueExpiredAt = p.EndDate;

        switch (p.Type)
        {
            case PromotionType.HappyHour:
            case PromotionType.WeeklySpecial:
                if (p.DailyEndTime.HasValue)
                {
                    trueExpiredAt = today.Add(p.DailyEndTime.Value);
                }
                else if (p.Type == PromotionType.WeeklySpecial)
                {
                    trueExpiredAt = today.AddDays(1).AddTicks(-1);
                }
                break;

            case PromotionType.Clearance:
            case PromotionType.Standard:
                trueExpiredAt = p.EndDate;
                break;
        }

        if (p.EndDate.HasValue && trueExpiredAt > p.EndDate.Value)
            trueExpiredAt = p.EndDate.Value;

        return trueExpiredAt;
    }


    public async Task CancelExpiredUnpaidOrdersAsync()
    {
        var expiredOrders = await _unitOfWork.Orders.GetExpiredUnpaidOrdersAsync(15);
        
        if (!expiredOrders.Any())
            return;

        var comboDishIds = expiredOrders
            .SelectMany(o => o.OrderDetails)
            .Where(od => od.Dish != null && od.Dish.Type == DishType.Combo)
            .Select(od => od.DishId)
            .Distinct()
            .ToList();

        var allComboDetails = comboDishIds.Any() 
            ? await _unitOfWork.ComboDetails.FindAsync(c => comboDishIds.Contains(c.DishId))
            : new List<ComboDetail>();

        var comboDetailsLookup = allComboDetails.ToLookup(c => c.DishId);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var refundsByRestaurant = new Dictionary<int, Dictionary<int, int>>();
            var transactionsToRemove = new List<Transaction>();
            var orderDetailsToRemove = new List<OrderDetail>();

            foreach (var order in expiredOrders)
            {
                if (!refundsByRestaurant.ContainsKey(order.RestaurantId))
                    refundsByRestaurant[order.RestaurantId] = new Dictionary<int, int>();

                var dishQuantitiesToRefund = refundsByRestaurant[order.RestaurantId];

                foreach (var detail in order.OrderDetails)
                {
                    if (dishQuantitiesToRefund.ContainsKey(detail.DishId))
                        dishQuantitiesToRefund[detail.DishId] += detail.Quantity;
                    else
                        dishQuantitiesToRefund[detail.DishId] = detail.Quantity;
                }

                var transactions = await _unitOfWork.Transactions.FindAsync(t => t.OrderId == order.Id);
                transactionsToRemove.AddRange(transactions);
                orderDetailsToRemove.AddRange(order.OrderDetails);
                
                _unitOfWork.Orders.Delete(order);
            }

            foreach (var kvp in refundsByRestaurant)
            {
                if (kvp.Value.Any())
                {
                    await _unitOfWork.BranchDishConfigs.RefundDishAvailabilityBatchAsync(kvp.Key, kvp.Value);
                }
            }

            if (transactionsToRemove.Any())
                _unitOfWork.Transactions.RemoveRange(transactionsToRemove);

            if (orderDetailsToRemove.Any())
                _unitOfWork.OrderDetails.RemoveRange(orderDetailsToRemove);

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi hủy hàng loạt đơn hàng chưa thanh toán quá hạn.");
        }
    }

    public async Task<bool> ConfirmPickupTimeAsync(ConfirmPickupTimeRequest request)
    {
        if (request.OrderId == Guid.Empty)
            throw new DomainException(OrderMessage.OrderError.INVALID_ORDER_ID);

        var order = await _unitOfWork.Orders.GetByIdAsync(request.OrderId);
        if (order == null)
            throw new DomainException(OrderMessage.OrderError.ORDER_NOT_FOUND);

        if (!order.IsPreOrder)
            throw new DomainException("Chỉ đơn hàng đặt trước (Pre-order) mới cần xác nhận thời gian nhận hàng.");

        if (request.ConfirmedPickupAt <= DateTime.UtcNow)
            throw new DomainException("Thời gian xác nhận phải sau thời điểm hiện tại.");

        order.ConfirmedPickupAt = request.ConfirmedPickupAt;
        _unitOfWork.Orders.Update(order);

        await _unitOfWork.SaveAsync();

        if (_realtimeService != null)
        {
            await _realtimeService.NotifyOrderStatusChanged(
                order.RestaurantId.ToString(),
                order.Id.ToString(),
                (int)order.Status
            );
        }

        return true;
    }

    public async Task<PagedResult<TenantOrderResponseDto>> GetTenantOrdersAsync(
        int restaurantId,
        int pageIndex,
        int pageSize,
        string? keyword = null,
        OrderStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        TypeOrder? typeOrder = null,
        RefundType? refundType = null)
    {
        var result = await _unitOfWork.Orders.GetTenantOrdersPagedAsync(
            restaurantId, pageIndex, pageSize, keyword, status, fromDate, toDate, typeOrder, refundType);

        var staffIds = result.Items
            .Where(o => o.ResponsibleStaffId.HasValue)
            .Select(o => o.ResponsibleStaffId!.Value)
            .Distinct()
            .ToList();

        var refundOrderIds = result.Items
            .Where(o => o.RefundOrderId.HasValue)
            .Select(o => o.RefundOrderId!.Value)
            .Distinct()
            .ToList();

        var staffNames = new Dictionary<Guid, string>();
        if (staffIds.Any())
        {
            var staffs = await _unitOfWork.Staffs.FindAsync(s => staffIds.Contains(s.Id));
            staffNames = staffs.ToDictionary(s => s.Id, s => s.Name);
        }

        var originalOrderCodes = new Dictionary<Guid, int>();
        if (refundOrderIds.Any())
        {
            var originalOrders = await _unitOfWork.Orders.FindAsync(o => refundOrderIds.Contains(o.Id));
            originalOrderCodes = originalOrders.ToDictionary(o => o.Id, o => o.OrderCode);
        }

        return new PagedResult<TenantOrderResponseDto>
        {
            Items = result.Items.Select(o => new TenantOrderResponseDto
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                NumberPhone = o.NumberPhone,
                TotalAmount = o.TotalAmount,
                PromotionDiscount = o.PromotionDiscount,
                FinalAmount = o.FinalAmount,
                Status = o.Status,
                IsPreOrder = o.IsPreOrder,
                RequestedPickupAt = o.RequestedPickupAt,
                ConfirmedPickupAt = o.ConfirmedPickupAt,
                Note = o.Note,
                Type = o.Type,
                PaymentProofUrl = o.PaymentProofUrl,
                TypeOrder = o.typeOrder,
                RefundType = o.RefundType,
                RefundOrderId = o.RefundOrderId,
                OriginalOrderCode = o.RefundOrderId.HasValue && originalOrderCodes.ContainsKey(o.RefundOrderId.Value) 
                    ? originalOrderCodes[o.RefundOrderId.Value] 
                    : null,
                ResponsibleStaffName = o.ResponsibleStaffId.HasValue && staffNames.ContainsKey(o.ResponsibleStaffId.Value)
                    ? staffNames[o.ResponsibleStaffId.Value]
                    : null,
                CreatedAt = o.CreatedAt,
                OrderDetails = o.OrderDetails?.Select(od => new TenantOrderDetailDto
                {
                    DishId = od.DishId,
                    DishName = od.Dish?.DishName ?? "Unknown",
                    Quantity = od.Quantity,
                    SubTotal = od.SubTotal,
                    OriginalPrice = od.OriginalPrice,
                    DiscountedPrice = od.DiscountedPrice,
                    PromotionAmount = od.PromotionAmount,
                    RefundedQuantity = od.RefundedQuantity
                }).ToList() ?? new List<TenantOrderDetailDto>()
            }),
            TotalCount = result.TotalCount,
            Page = pageIndex,
            PageSize = pageSize
        };
    }
}

