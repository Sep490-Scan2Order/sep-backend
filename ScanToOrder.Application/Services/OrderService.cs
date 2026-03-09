using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartRedisService _cartRedisService;

    public OrderService(
        IUnitOfWork unitOfWork,
        ICartRedisService cartRedisService)
    {
        _unitOfWork = unitOfWork;
        _cartRedisService = cartRedisService;
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
        var dto = new CartDto
        {
            CartId = cart.CartId,
            RestaurantId = cart.RestaurantId,
            TotalAmount = cart.TotalAmount,
            Items = cart.Items.Select(i => new CartItemModel
            {
                DishId = i.DishId,
                DishName = i.DishName,
                Quantity = i.Quantity,
                Price = i.Price,
                SubTotal = i.SubTotal
            }).ToList()
        };

        return dto;
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

        return new CartDto
        {
            CartId = cart.CartId,
            RestaurantId = cart.RestaurantId,
            TotalAmount = cart.TotalAmount,
            Items = cart.Items.Select(i => new CartItemModel
            {
                DishId = i.DishId,
                DishName = i.DishName,
                Quantity = i.Quantity,
                Price = i.Price,
                SubTotal = i.SubTotal
            }).ToList()
        };
    }
}

