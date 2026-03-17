using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private readonly IMapper _mapper;
    private readonly IAuthenticatedUserService _authenticatedUserService;
    private readonly IStorageService _storageService;
    private readonly ILogger<OrderService> _logger;
    private readonly IQrCodeService _qrCodeService;

    public OrderService(
        IUnitOfWork unitOfWork,
        ICartRedisService cartRedisService,
        ITransactionRedisService transactionRedisService,
        IRealtimeService realtimeService,
        IMapper mapper,
        IAuthenticatedUserService authenticatedUserService,
        IStorageService storageService,
        ILogger<OrderService> logger,
        IQrCodeService qrCodeService)
    {
        _unitOfWork = unitOfWork;
        _cartRedisService = cartRedisService;
        _transactionRedisService = transactionRedisService;
        _realtimeService = realtimeService;
        _mapper = mapper;
        _authenticatedUserService = authenticatedUserService;
        _storageService = storageService;
        _logger = logger;
        _qrCodeService = qrCodeService;
    }

    public async Task<CartDto> AddToCartAsync(AddToCartRequest request)
    {
        if (request.Quantity <= 0)
        {
            throw new DomainException(OrderMessage.OrderError.QUANTITY_MUST_BE_GREATER_THAN_ZERO);
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

        if (!branchDish.IsSelling)
        {
            throw new DomainException(BranchDishMessage.BranchDishError.NOT_SELLING);
        }

        if (branchDish.IsSoldOut)
        {
            throw new DomainException(BranchDishMessage.BranchDishError.SOLD_OUT);
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
        return _mapper.Map<CartDto>(cart);
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

    public async Task<PaymentQrDto> GetPaymentQrAsync(string cartId, string phone)
    {
        if (string.IsNullOrWhiteSpace(cartId))
            throw new DomainException(OrderMessage.OrderError.CART_ID_REQUIRED);

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

        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException(OrderMessage.OrderError.PHONE_REQUIRED);

        var amount = Math.Round(cart.TotalAmount);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        Guid orderId;
        string qrOrderUrl;
        try
        {
            foreach (var item in cart.Items)
            {
                var reserved = await _unitOfWork.BranchDishConfigs
                    .ReserveDishAvailabilityAsync(cart.RestaurantId, item.DishId, item.Quantity);

                if (!reserved)
                {
                    throw new DomainException(string.Format(OrderMessage.OrderError.DISH_OUT_OF_STOCK, item.DishName));
                }
            }

            var (startUtc, endUtc, dateInt) = GetVietnamDayRangeUtc();
            int orderCode = await _unitOfWork.Orders.GetNextDailyOrderCodeAsync(
                cart.RestaurantId, startUtc, endUtc, dateInt);

            orderId = Guid.NewGuid();
            string qrContent = orderId.ToString();
            var qrBytes = _qrCodeService.GenerateQrCodeBytes(qrContent);

            qrOrderUrl = await _storageService.UploadOrderQrAsync(qrBytes, orderId);
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
                SubTotal = i.SubTotal
            }).ToList();

            await _unitOfWork.OrderDetails.AddRangeAsync(details);

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();
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
            await _realtimeService.SendOrderToKitchen(
         order.RestaurantId.ToString(),
         orderRealtime
     );
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
            OrderId = orderId,
            QrUrl = qrUrl,
            PaymentCode = paymentCode,
            TotalAmount = amount,
            RestaurantName = restaurant.RestaurantName ?? "",
            QrCodeBase64 = qrOrderUrl
        };
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

        var amount = Math.Round(cart.TotalAmount);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        Guid orderId;
        string qrOrderUrl;

        try
        {
            foreach (var item in cart.Items)
            {
                var reserved = await _unitOfWork.BranchDishConfigs
                    .ReserveDishAvailabilityAsync(cart.RestaurantId, item.DishId, item.Quantity);

                if (!reserved)
                {
                    throw new DomainException(string.Format(OrderMessage.OrderError.DISH_OUT_OF_STOCK, item.DishName));
                }
            }

            var (startUtc, endUtc, dateInt) = GetVietnamDayRangeUtc();
            int orderCode = await _unitOfWork.Orders.GetNextDailyOrderCodeAsync(
                cart.RestaurantId, startUtc, endUtc, dateInt);

            orderId = Guid.NewGuid();
            string qrContent = orderId.ToString();
            var qrBytes = _qrCodeService.GenerateQrCodeBytes(qrContent);

            qrOrderUrl = await _storageService.UploadOrderQrAsync(qrBytes, orderId);
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
                PaymentMethod = PaymentMethod.Cash
            });
         
            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();
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
            await _realtimeService.SendOrderToKitchen(
         order.RestaurantId.ToString(),
         orderRealtime
     );
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
            Note = cart.Note,
            QrCodeBase64 = qrOrderUrl
        };

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

        var activeShift = await _unitOfWork.Shifts.FirstOrDefaultAsync(
            s => s.RestaurantId == order.RestaurantId && s.Status == ShiftStatus.Open);

        if (activeShift == null)
            throw new DomainException(ShiftMessage.ShiftError.SHIFT_NOT_FOUND);

        if (activeShift.StaffId != staff.Id)
            throw new DomainException(StaffMessage.StaffError.UNAUTHORIZED_ACCESS);

        var transaction = await _unitOfWork.Transactions.FirstOrDefaultAsync(
            t => t.OrderId == orderId && t.PaymentMethod == PaymentMethod.Cash);

        if (transaction == null)
            throw new DomainException(OrderMessage.OrderError.CASH_TRANSACTION_NOT_FOUND);

        if (transaction.Status == OrderTransactionStatus.Success)
        {
            return;
        }

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        try
        {
            order.Status = OrderStatus.Pending; 
            _unitOfWork.Orders.Update(order);

            transaction.ShiftId = activeShift.Id;
            transaction.Status = OrderTransactionStatus.Success;
            _unitOfWork.Transactions.Update(transaction);

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();
            string audioUrl = string.Empty;
            if (_realtimeService != null)
            {
                await _realtimeService.NotifyOrderStatusChanged(
                    order.RestaurantId.ToString(),
                    order.Id.ToString(),
                    (int)order.Status
                );
            }
            try
            {
                audioUrl = await _storageService.GetOrGeneratePaymentReceivedAudioAsync(order.OrderCode, transaction.TotalAmount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tạo audio thông báo đã nhận chuyển khoản thất bại. OrderCode={OrderCode}, Amount={Amount}", order.OrderCode, transaction.TotalAmount);
            }
            await _realtimeService.NotifyPaymentReceived(order.RestaurantId.ToString(), order.OrderCode, transaction.TotalAmount, audioUrl);
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
            Phone = o.NumberPhone,
            Note = o.Note,
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

        var orderIdString = await _transactionRedisService.GetCartIdByOrderPaymentCodeAsync(paymentCode);
        if (string.IsNullOrWhiteSpace(orderIdString))
            throw new DomainException(OrderMessage.OrderError.ORDER_FROM_PAYMENT_CODE_NOT_FOUND_OR_EXPIRED);

        if (!Guid.TryParse(orderIdString, out var orderId))
            throw new DomainException(OrderMessage.OrderError.INVALID_ORDER_CODE);

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

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        try
        {
            order.Status = OrderStatus.Pending;
            _unitOfWork.Orders.Update(order);
           
            transaction.Status = OrderTransactionStatus.Success;
            _unitOfWork.Transactions.Update(transaction);

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();
            if (_realtimeService != null)
            {
                await _realtimeService.NotifyOrderStatusChanged(
                    order.RestaurantId.ToString(),
                    order.Id.ToString(),
                    (int)order.Status
                );
                string audioUrl = string.Empty;
                try
                {
                    audioUrl = await _storageService.GetOrGeneratePaymentReceivedAudioAsync(order.OrderCode, transferAmount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tạo audio thông báo đã nhận chuyển khoản thất bại. OrderCode={OrderCode}, Amount={Amount}", order.OrderCode, transferAmount);
                }
                await _realtimeService.NotifyPaymentReceived(order.RestaurantId.ToString(), order.OrderCode, transferAmount, audioUrl);
            }
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
                OriginalPrice = od.OriginalPrice,
                DiscountedPrice = od.DiscountedPrice,
                PromotionAmount = od.PromotionAmount,
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
            await _realtimeService.NotifyCustomerOrderStatusChanged(order.Id.ToString(), (int)newStatus);
        }
        return true;
    }

    public async Task<List<CustomerOrderSummaryDto>> GetCustomerOrdersAsync(int restaurantId, string phone, int limit = 20)
    {
        if (restaurantId <= 0)
            throw new DomainException("RestaurantId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("Số điện thoại không được để trống.");

        limit = Math.Clamp(limit, 1, 50);
        phone = phone.Trim();

        var orders = await _unitOfWork.Orders.GetRecentByRestaurantAndPhoneAsync(restaurantId, phone, limit);

        return orders.Select(o => new CustomerOrderSummaryDto
        {
            OrderId = o.Id,
            OrderCode = o.OrderCode,
            Status = o.Status,
            CreatedAt = o.CreatedAt,
            FinalAmount = o.FinalAmount,
            QrCodeUrl = o.QrCodeUrl
        }).ToList();
    }
    
    public async Task<List<MenuDishItemDto>> GetDishesByIdsWithPromotionAsync(int restaurantId, List<int> dishIds)
    {
        if (dishIds == null || !dishIds.Any())
            throw new DomainException(OrderMessage.OrderError.DISH_ID_LIST_REQUIRED);

        var now = TimeUtils.GetVietnamTimeNow();

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

                // Round to the nearest thousand (e.g. 21999 -> 22000)
                discountedPrice = PricingUtils.RoundToNearestThousand(discountedPrice);

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
                DishAvailabilityStock = bdc.DishAvailability,
                ExpiredAt = winningPromo != null ? CalculateTrueExpiredAt(winningPromo, now) : null,
                IsSoldOut = bdc.IsSoldOut
            };
        }).ToList();

        return result;
    }

    public async Task<bool> ValidateQrCodeAsync(string qrContent)
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

        return true;
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

    public async Task CancelExpiredUnpaidOrdersAsync()
    {
        var expiredOrders = await _unitOfWork.Orders.GetExpiredUnpaidOrdersAsync(10);
        
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

        foreach (var order in expiredOrders)
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                order.Status = OrderStatus.Cancelled;
                _unitOfWork.Orders.Update(order);

                var dishQuantitiesToRefund = new Dictionary<int, int>();

                foreach (var detail in order.OrderDetails)
                {
                    if (dishQuantitiesToRefund.ContainsKey(detail.DishId))
                        dishQuantitiesToRefund[detail.DishId] += detail.Quantity;
                    else
                        dishQuantitiesToRefund[detail.DishId] = detail.Quantity;

                    if (detail.Dish != null && detail.Dish.Type == DishType.Combo)
                    {
                        var comboItems = comboDetailsLookup[detail.DishId];
                        foreach (var comboItem in comboItems)
                        {
                            var qty = detail.Quantity * comboItem.Quantity;
                            if (dishQuantitiesToRefund.ContainsKey(comboItem.ItemDishId))
                                dishQuantitiesToRefund[comboItem.ItemDishId] += qty;
                            else
                                dishQuantitiesToRefund[comboItem.ItemDishId] = qty;
                        }
                    }
                }

                if (dishQuantitiesToRefund.Any())
                {
                    await _unitOfWork.BranchDishConfigs.RefundDishAvailabilityBatchAsync(order.RestaurantId, dishQuantitiesToRefund);
                }

                await _unitOfWork.SaveAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi hủy đơn hàng chưa thanh toán quá hạn: {OrderId}", order.Id);
            }
        }
    }
}

