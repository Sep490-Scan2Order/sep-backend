using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Utils;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartRedisService _cartRedisService;
    private readonly ITransactionRedisService _transactionRedisService;
    private readonly IMapper _mapper;

    public OrderService(
        IUnitOfWork unitOfWork,
        ICartRedisService cartRedisService,
        ITransactionRedisService transactionRedisService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _cartRedisService = cartRedisService;
        _transactionRedisService = transactionRedisService;
        _mapper = mapper;
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

        // 8. Build DTO trả về cho FE 
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

        return _mapper.Map<CartDto>(cart);
    }

    public async Task<PaymentQrDto> GetPaymentQrAsync(string cartId)
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

        var amount = Math.Round(cart.TotalAmount);
        var (qrUrl, paymentCode) = BankQrLinkUtils.GenerateSePayQrUrl(
            tenant.CardNumber,
            tenant.Bank.ShortName,
            amount,
            PaymentIntent.OrderPayment);

        await _transactionRedisService.SaveOrderPaymentCodeAsync(paymentCode, cartId);

        return new PaymentQrDto
        {
            QrUrl = qrUrl,
            PaymentCode = paymentCode,
            TotalAmount = amount,
            RestaurantName = restaurant.RestaurantName ?? ""
        };
    }

    public async Task ProcessOrderPaymentAsync(string paymentCode, decimal transferAmount)
    {
        if (string.IsNullOrWhiteSpace(paymentCode))
            throw new DomainException("PaymentCode không được để trống.");

        if (transferAmount <= 0)
            throw new DomainException("Số tiền thanh toán không hợp lệ.");

        var existed = await _unitOfWork.Transactions.ExistsAsync(t => t.TransactionCode == paymentCode);
        if (existed)
        {
            return;
        }

        var cartId = await _transactionRedisService.GetCartIdByOrderPaymentCodeAsync(paymentCode);
        if (string.IsNullOrWhiteSpace(cartId))
            throw new DomainException("Không tìm thấy cartId từ mã thanh toán hoặc đã hết hạn.");

        var json = await _cartRedisService.GetRawCartAsync(cartId);
        if (string.IsNullOrEmpty(json))
            throw new DomainException("Giỏ hàng không tồn tại hoặc đã hết hạn.");

        var cart = JsonSerializer.Deserialize<CartModel>(json)
                   ?? throw new DomainException("Dữ liệu giỏ hàng không hợp lệ.");

        if (cart.Items == null || !cart.Items.Any())
            throw new DomainException("Giỏ hàng trống, không thể tạo đơn hàng.");

        var expectedAmount = Math.Round(cart.TotalAmount);
        if (Math.Round(transferAmount) < expectedAmount)
            throw new DomainException("Số tiền thanh toán không khớp với tổng tiền giỏ hàng.");

        var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(cart.RestaurantId);
        if (restaurant == null)
            throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var (startUtc, endUtc, dateInt) = GetVietnamDayRangeUtc();
            int orderCode = await _unitOfWork.Orders.GetNextDailyOrderCodeAsync(cart.RestaurantId, startUtc, endUtc, dateInt);

            var orderId = Guid.NewGuid();
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
                Status = OrderStatus.Preparing,
                IsScanned = false,
                Type = "SePay",
                UserId = null
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

            var transaction = new Transaction
            {
                OrderId = orderId,
                Status = OrderTransactionStatus.Success,
                TotalAmount = expectedAmount,
                TransactionCode = paymentCode,
                PaymentMethod = PaymentMethod.BankTransfer
            };

            await _unitOfWork.Transactions.AddAsync(transaction);

            await _unitOfWork.SaveAsync();
            await tx.CommitAsync();

            await _cartRedisService.DeleteCartAsync(cartId);
            await _transactionRedisService.DeleteOrderPaymentCodeAsync(paymentCode);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
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

