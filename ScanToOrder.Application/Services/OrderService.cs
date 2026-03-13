using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using DocumentFormat.OpenXml.InkML;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Utils;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartRedisService _cartRedisService;
    private readonly ITransactionRedisService _transactionRedisService;
    private readonly IRealtimeService _realtimeService;
    private readonly IMapper _mapper;
    private readonly IAuthenticatedUserService _authenticatedUserService;

    public OrderService(
        IUnitOfWork unitOfWork,
        ICartRedisService cartRedisService,
        ITransactionRedisService transactionRedisService,
        IRealtimeService realtimeService,
        IMapper mapper,
        IAuthenticatedUserService authenticatedUserService)
    {
        _unitOfWork = unitOfWork;
        _cartRedisService = cartRedisService;
        _transactionRedisService = transactionRedisService;
        _realtimeService = realtimeService;
        _mapper = mapper;
        _authenticatedUserService = authenticatedUserService;
    }

    public async Task<CartDto> AddToCartAsync(AddToCartRequest request)
    {
        if (request.Quantity <= 0)
        {
            throw new DomainException("Số lượng phải lớn hơn 0.");
        }

        // 1. Kiểm tra nhà hàng tồn tại
        var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(request.RestaurantId);
        if (restaurant == null)
        {
            throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
        }

        // 2. Lấy cấu hình món theo chi nhánh để đảm bảo đúng nhà hàng
        var branchDish =
            await _unitOfWork.BranchDishConfigs.FirstOrDefaultAsync(b =>
                b.RestaurantId == request.RestaurantId && b.DishId == request.DishId);

        if (branchDish == null)
        {
            throw new DomainException(DishMessage.DishError.DISH_NOT_FOUND);
        }

        if (!branchDish.IsSelling || branchDish.IsSoldOut)
        {
            throw new DomainException("Món ăn hiện không còn bán tại nhà hàng này.");
        }

        // 3. Lấy thông tin món ăn để hiển thị trong DTO
        var dish = await _unitOfWork.Dishes.GetByIdAsync(request.DishId);
        if (dish == null)
        {
            throw new DomainException(DishMessage.DishError.DISH_NOT_FOUND);
        }

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
                throw new DomainException("Không thể thêm món của nhà hàng khác vào cùng một giỏ hàng.");
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
                Price = branchDish.Price,
                OriginalPrice = branchDish.Price,
                SubTotal = branchDish.Price * request.Quantity
            };
            cart.Items.Add(existingItem);
        }
        else
        {
            existingItem.Quantity += request.Quantity;
            existingItem.SubTotal = existingItem.Price * existingItem.Quantity;
        }

        // 6. Tính lại tổng tiền giỏ
        cart.TotalAmount = cart.Items.Sum(i => i.SubTotal);

        // 7. Lưu lại cart lên Redis dưới dạng JSON string
        var json = JsonSerializer.Serialize(cart);
        await _cartRedisService.SaveRawCartAsync(cartId, json, TimeSpan.FromMinutes(60));

        // 8. Đồng bộ lại giá/khuyến mãi/tồn kho trước khi trả về
        cart = await SyncCartPricingAndAvailabilityAsync(cart);

        // 9. Trả về full CartDto 
        return _mapper.Map<CartDto>(cart);
    }

    public async Task<CartDto> GetCartAsync(string cartId)
    {
        if (string.IsNullOrWhiteSpace(cartId))
        {
            throw new DomainException("CartId không được để trống.");
        }

        var json = await _cartRedisService.GetRawCartAsync(cartId);
        if (string.IsNullOrEmpty(json))
        {
            throw new DomainException("Giỏ hàng không tồn tại hoặc đã hết hạn.");
        }

        var cart = JsonSerializer.Deserialize<CartModel>(json)
                   ?? throw new DomainException("Dữ liệu giỏ hàng không hợp lệ.");

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

            if (item.Price != dishInfo.DiscountedPrice)
            {
                if (item.OriginalPrice == 0)
                {
                    item.OriginalPrice = dishInfo.Price;
                }

                item.Price = dishInfo.DiscountedPrice;
                item.DiscountAmount = (item.OriginalPrice - item.Price) * item.Quantity;
                item.PromotionName = dishInfo.PromotionName;
                item.SubTotal = item.Price * item.Quantity;
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
                    item.SubTotal = item.Price * item.Quantity;
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

    public async Task<PaymentQrDto> GetPaymentQrAsync(string cartId, string phone)
    {
        if (string.IsNullOrWhiteSpace(cartId))
            throw new DomainException("CartId không được để trống.");

        var json = await _cartRedisService.GetRawCartAsync(cartId);
        if (string.IsNullOrEmpty(json))
            throw new DomainException("Giỏ hàng không tồn tại hoặc đã hết hạn.");

        var cart = JsonSerializer.Deserialize<CartModel>(json)
                   ?? throw new DomainException("Dữ liệu giỏ hàng không hợp lệ.");

        if (cart.Items == null || !cart.Items.Any())
            throw new DomainException("Giỏ hàng trống, không thể tạo mã thanh toán.");

        var restaurant = await _unitOfWork.Restaurants.GetByIdWithTenantBankAsync(cart.RestaurantId);
        if (restaurant?.Tenant == null)
            throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

        var tenant = restaurant.Tenant;
        if (tenant.BankId == null || tenant.Bank == null || string.IsNullOrWhiteSpace(tenant.CardNumber))
            throw new DomainException("Nhà hàng chưa cấu hình tài khoản ngân hàng để nhận thanh toán.");

        if (!tenant.IsVerifyBank)
            throw new DomainException("Tài khoản ngân hàng của nhà hàng chưa được xác thực.");

        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("Số điện thoại không được để trống.");

        var amount = Math.Round(cart.TotalAmount);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        Guid orderId;
        try
        {
            foreach (var item in cart.Items)
            {
                var reserved = await _unitOfWork.BranchDishConfigs
                    .ReserveDishAvailabilityAsync(cart.RestaurantId, item.DishId, item.Quantity);

                if (!reserved)
                {
                    throw new DomainException($"Món {item.DishName} đã hết số lượng.");
                }
            }

            var (startUtc, endUtc, dateInt) = GetVietnamDayRangeUtc();
            int orderCode = await _unitOfWork.Orders.GetNextDailyOrderCodeAsync(
                cart.RestaurantId, startUtc, endUtc, dateInt);

            orderId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId,
                RestaurantId = cart.RestaurantId,
                OrderCode = orderCode,
                IsPreOrder = false,
                Note = cart.Note,
                TotalAmount = cart.TotalAmount,
                PromotionDiscount = 0,
                FinalAmount = cart.TotalAmount,
                Status = OrderStatus.Unpaid,
                IsScanned = false,
                Type = "SePay",
                NumberPhone = phone
            };

            await _unitOfWork.Orders.AddAsync(order);

            var details = cart.Items.Select(i => new OrderDetail
            {
                OrderId = orderId,
                DishId = i.DishId,
                Quantity = i.Quantity,
                Price = i.Price,
                SubTotal = i.SubTotal
            }).ToList();

            await _unitOfWork.OrderDetails.AddRangeAsync(details);

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        await _cartRedisService.DeleteCartAsync(cartId);

        var (qrUrl, paymentCode) = BankQrLinkUtils.GenerateSePayQrUrl(
            tenant.CardNumber,
            tenant.Bank.ShortName,
            amount,
            PaymentIntent.OrderPayment);

        await _unitOfWork.Transactions.AddAsync(new Transaction
        {
            OrderId = orderId,
            Status = OrderTransactionStatus.Pending,
            TotalAmount = amount,
            TransactionCode = paymentCode,
            PaymentMethod = PaymentMethod.BankTransfer
        });

        await _unitOfWork.SaveAsync();

        await _transactionRedisService.SaveOrderPaymentCodeAsync(paymentCode, orderId.ToString());

        return new PaymentQrDto
        {
            QrUrl = qrUrl,
            PaymentCode = paymentCode,
            TotalAmount = amount,
            RestaurantName = restaurant.RestaurantName ?? ""
        };
    }

    public async Task<CashCheckoutResponse> CheckoutCashAsync(CashCheckoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CartId))
            throw new DomainException("CartId không được để trống.");

        if (string.IsNullOrWhiteSpace(request.Phone))
            throw new DomainException("Số điện thoại không được để trống.");

        var json = await _cartRedisService.GetRawCartAsync(request.CartId);
        if (string.IsNullOrEmpty(json))
            throw new DomainException("Giỏ hàng không tồn tại hoặc đã hết hạn.");

        var cart = JsonSerializer.Deserialize<CartModel>(json)
                   ?? throw new DomainException("Dữ liệu giỏ hàng không hợp lệ.");

        if (cart.Items == null || !cart.Items.Any())
            throw new DomainException("Giỏ hàng trống, không thể tạo đơn hàng.");

        var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(cart.RestaurantId);
        if (restaurant == null)
            throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

        var amount = Math.Round(cart.TotalAmount);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        Guid orderId;
        try
        {
            foreach (var item in cart.Items)
            {
                var reserved = await _unitOfWork.BranchDishConfigs
                    .ReserveDishAvailabilityAsync(cart.RestaurantId, item.DishId, item.Quantity);

                if (!reserved)
                {
                    throw new DomainException($"Món {item.DishName} đã hết số lượng.");
                }
            }

            var (startUtc, endUtc, dateInt) = GetVietnamDayRangeUtc();
            int orderCode = await _unitOfWork.Orders.GetNextDailyOrderCodeAsync(
                cart.RestaurantId, startUtc, endUtc, dateInt);

            orderId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId,
                RestaurantId = cart.RestaurantId,
                OrderCode = orderCode,
                IsPreOrder = false,
                Note = cart.Note,
                TotalAmount = cart.TotalAmount,
                PromotionDiscount = 0,
                FinalAmount = cart.TotalAmount,
                Status = OrderStatus.Unpaid,
                IsScanned = false,
                Type = "Cash",
                NumberPhone = request.Phone
            };

            await _unitOfWork.Orders.AddAsync(order);

            var details = cart.Items.Select(i => new OrderDetail
            {
                OrderId = orderId,
                DishId = i.DishId,
                Quantity = i.Quantity,
                Price = i.Price,
                SubTotal = i.SubTotal
            }).ToList();

            await _unitOfWork.OrderDetails.AddRangeAsync(details);

            await _unitOfWork.Transactions.AddAsync(new Transaction
            {
                OrderId = orderId,
                Status = OrderTransactionStatus.Pending,
                TotalAmount = amount,
                TransactionCode = null,
                PaymentMethod = PaymentMethod.Cash
            });

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        await _cartRedisService.DeleteCartAsync(request.CartId);

        return new CashCheckoutResponse
        {
            OrderId = orderId,
            OrderCode = (await _unitOfWork.Orders.GetByIdAsync(orderId))!.OrderCode,
            TotalAmount = amount,
            RestaurantName = restaurant.RestaurantName,
            Phone = request.Phone,
            Note = cart.Note
        };
    }

    public async Task ConfirmCashPaymentAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
            throw new DomainException("OrderId không hợp lệ.");

        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            throw new DomainException("Đơn hàng không tồn tại.");

        if (order.Status != OrderStatus.Unpaid)
        {           
            return;
        }

        var transaction = await _unitOfWork.Transactions.FirstOrDefaultAsync(
            t => t.OrderId == orderId && t.PaymentMethod == PaymentMethod.Cash);

        if (transaction == null)
            throw new DomainException("Giao dịch tiền mặt không tồn tại.");

        if (transaction.Status == OrderTransactionStatus.Success)
        {
            return;
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
            throw new DomainException("Không xác định được nhân viên đăng nhập.");

        var staff = await _unitOfWork.Staffs.GetByIdAsync(_authenticatedUserService.ProfileId.Value);
        if (staff == null)
            throw new DomainException(StaffMessage.StaffError.STAFF_NOT_FOUND);

        var restaurantId = staff.RestaurantId;

        var orders = await _unitOfWork.Orders.GetCashOrdersPendingConfirmAsync(restaurantId);
        if (orders == null || !orders.Any()) return new List<CashPendingOrderResponse>();

        return orders.Select(order => new CashPendingOrderResponse
        {
            Id = order.Id.ToString(),
            OrderCode = order.OrderCode,
            RestaurantId = order.RestaurantId,
            CreatedAt = order.CreatedAt,
            Amount = order.FinalAmount,
            Phone = order.NumberPhone,
            Note = order.Note,
            Status = (int)order.Status
        }).ToList();
    }

    public async Task ProcessOrderPaymentAsync(string paymentCode, decimal transferAmount)
    {
        if (string.IsNullOrWhiteSpace(paymentCode))
            throw new DomainException("PaymentCode không được để trống.");

        if (transferAmount <= 0)
            throw new DomainException("Số tiền thanh toán không hợp lệ.");

        var transaction = await _unitOfWork.Transactions.FirstOrDefaultAsync(
            t => t.TransactionCode == paymentCode);
        if (transaction == null)
            throw new DomainException("Giao dịch không tồn tại.");

        if (transaction.Status == OrderTransactionStatus.Success)
        {
            return;
        }

        var orderIdString = await _transactionRedisService.GetCartIdByOrderPaymentCodeAsync(paymentCode);
        if (string.IsNullOrWhiteSpace(orderIdString))
            throw new DomainException("Không tìm thấy đơn hàng từ mã thanh toán hoặc đã hết hạn.");

        if (!Guid.TryParse(orderIdString, out var orderId))
            throw new DomainException("Mã đơn hàng không hợp lệ.");

        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            throw new DomainException("Đơn hàng không tồn tại.");

        if (order.Status != OrderStatus.Unpaid)
        {
            return;
        }

        var expectedAmount = Math.Round(order.FinalAmount);
        if (Math.Round(transferAmount) < expectedAmount)
            throw new DomainException("Số tiền thanh toán không khớp với tổng tiền đơn hàng.");

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        try
        {
            order.Status = OrderStatus.Pending;
            _unitOfWork.Orders.Update(order);

            transaction.Status = OrderTransactionStatus.Success;
            _unitOfWork.Transactions.Update(transaction);

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();

            await _transactionRedisService.DeleteOrderPaymentCodeAsync(paymentCode);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<KdsOrderResponse>> GetKdsActiveOrders(int restaurantId)
    {


        var orders = await _unitOfWork.Orders.GetOrdersForKdsAsync(restaurantId);

        if (orders == null || !orders.Any()) return new List<KdsOrderResponse>();

        return orders.Select(order => new KdsOrderResponse
        {
            Id = order.Id.ToString(),
            OrderCode = order.OrderCode,
            CreatedAt = order.CreatedAt,
            Amount = order.FinalAmount,
            Phone = order.NumberPhone,
            Status = (int)order.Status,

            Items = order.OrderDetails.Select(od => new KdsItemResponse
            {
                Id = od.Id.ToString(),
                Name = od.Dish.DishName,
                Price = od.Price,
                Quantity = od.Quantity,
                Image = od.Dish.ImageUrl
            }).ToList()
        }).ToList();
    }
    public async Task<bool> UpdateOrderStatus(Guid orderId, OrderStatus newStatus)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null) return false;

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
        }
        return true;
    }
    
    // Get list of dishes with promotion info for given dishIds in a restaurant, used for FE to display correct price and promotion label when user add to cart
    public async Task<List<MenuDishItemDto>> GetDishesByIdsWithPromotionAsync(int restaurantId, List<int> dishIds)
    {
        if (dishIds == null || !dishIds.Any())
            throw new DomainException("Danh sách DishId không được để trống.");

        var now = DateTime.UtcNow.AddHours(7);

        var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId)
                         ?? throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

        var tenantId = restaurant.TenantId;

        var basePromotions = await _unitOfWork.Promotions.GetAllAsync(p =>
            p.TenantId == tenantId &&
            p.IsActive &&
            !p.IsDeleted &&
            p.Scope == PromotionScope.Dish &&
            (p.IsGlobal || (p.RestaurantPromotions.Any(rp => rp.RestaurantId == restaurantId)
                            && !p.PromotionDishes.Any()))
        );

        var branchDishes = await _unitOfWork.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(restaurantId, dishIds);

        var result = branchDishes.Select(bdc =>
        {
            var specificDishPromos = bdc.Dish.PromotionDishes?
                                         .Select(pd => pd.Promotion)
                                         .Where(p => p.Scope == PromotionScope.Dish &&
                                                     p.IsActive &&
                                                     !p.IsDeleted)
                                     ?? [];

            var allEligiblePromotions = basePromotions.Concat(specificDishPromos);

            var winningPromo = allEligiblePromotions
                .Where(p => p.IsValidAt(now))
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
                    : $"-{(winningPromo.DiscountValue / 1000):G}k";
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
                DishAvailabilityStock = bdc.DishAvailability,
                ExpiredAt = winningPromo != null ? CalculateTrueExpiredAt(winningPromo, now) : null,
                IsSoldOut = bdc.IsSoldOut
            };
        }).ToList();

        return result;
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

    private static (DateTime StartUtc, DateTime EndUtc, int DateInt) GetVietnamDayRangeUtc()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowVn = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            var vnDate = nowVn.Date;
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(vnDate, tz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(vnDate.AddDays(1), tz);
            int dateInt = (vnDate.Year * 10000) + (vnDate.Month * 100) + vnDate.Day;
            return (startUtc, endUtc, dateInt);
        }
        catch
        {
            var utcDate = DateTime.UtcNow.Date;
            int dateInt = (utcDate.Year * 10000) + (utcDate.Month * 100) + utcDate.Day;
            return (utcDate, utcDate.AddDays(1), dateInt);
        }
    }
}

