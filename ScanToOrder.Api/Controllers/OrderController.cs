using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers;

public class OrderController : BaseController
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost("add-to-cart")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> AddToCart([FromBody] AddToCartRequest request)
    {
        var result = await _orderService.AddToCartAsync(request);
        return Success(result, "Thêm món vào giỏ hàng thành công.");
    }

    [HttpGet("cart/{cartId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> GetCart([FromRoute] string cartId)
    {
        var result = await _orderService.GetCartAsync(cartId);
        return Success(result);
    }

    [HttpGet("payment/qr")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PaymentQrDto>>> GetPaymentQr([FromQuery] string cartId, string phone)
    {
        var result = await _orderService.GetPaymentQrAsync(cartId, phone);
        return Success(result);
    }

    [HttpGet("kds/active-orders/{restaurantId}")]
    public async Task<ActionResult<ApiResponse<List<KdsOrderResponse>>>> GetKdsActiveOrders([FromRoute] int restaurantId)
    {
        var result = await _orderService.GetKdsActiveOrders(restaurantId);
        return Success(result);
    }

    [HttpPost("dishes-with-promotion")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<MenuDishItemDto>>>> GetDishesByIdsWithPromotion(
        [FromBody] GetDishesByIdsRequest request)
    {
        var result = await _orderService.GetDishesByIdsWithPromotionAsync(request.RestaurantId, request.DishIds);
        return Success(result);
    }
}

