using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Orders;
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
}

