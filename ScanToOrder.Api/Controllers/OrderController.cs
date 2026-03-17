using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Api.Controllers;

public class OrderController : BaseController
{
    private readonly IOrderService _orderService;
    private readonly IStorageService _storageService;
    public OrderController(IOrderService orderService, IStorageService storageService)
    {
        _orderService = orderService;
        _storageService = storageService;
    }

    [HttpPost("add-to-cart")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CartDto>>> AddToCart([FromBody] AddToCartRequest request)
    {
        var result = await _orderService.AddToCartAsync(request);
        return Success(result, "Thêm món vào giỏ hàng thành công.");
    }

    // [HttpGet("cart/{cartId}")]
    // [AllowAnonymous]
    // public async Task<ActionResult<ApiResponse<CartDto>>> GetCart([FromRoute] string cartId)
    // {
    //     var result = await _orderService.GetCartAsync(cartId);
    //     return Success(result);
    // }

    [HttpPost("checkout/bank-transfer")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PaymentQrDto>>> GetPaymentQr([FromBody] PaymentQrRequest request)
    {
        var result = await _orderService.GetPaymentQrAsync(request.CartId, request.Phone);
        return Success(result);
    }

    [HttpPost("checkout/cash")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CashCheckoutResponse>>> CheckoutCash([FromBody] CashCheckoutRequest request)
    {
        var result = await _orderService.CheckoutCashAsync(request);
        return Success(result, "Tạo đơn thanh toán tiền mặt thành công.");
    }

    [HttpPost("cash/{orderId:guid}/confirm")]
    [Authorize(Roles = "Staff, Cashier")]
    public async Task<ActionResult<ApiResponse<string>>> ConfirmCashPayment([FromRoute] Guid orderId)
    {
        await _orderService.ConfirmCashPaymentAsync(orderId);
        return Success("Xác nhận thanh toán tiền mặt thành công.");
    }

    [HttpGet("cash/pending-confirm")]
    [Authorize(Roles = "Staff, Cashier")]
    public async Task<ActionResult<ApiResponse<List<CashPendingOrderResponse>>>> GetCashOrdersPendingConfirm()
    {
        var result = await _orderService.GetCashOrdersPendingConfirmAsync();
        return Success(result);
    }

    [HttpGet("kds/active-orders/{restaurantId}")]
    public async Task<ActionResult<ApiResponse<List<KdsOrderResponse>>>> GetKdsActiveOrders([FromRoute] int restaurantId)
    {
        var result = await _orderService.GetKdsActiveOrders(restaurantId);
        return Success(result);
    }


    [HttpPut("update-status/{orderId}")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateOrderStatus([FromRoute] Guid orderId, [FromQuery] OrderStatus newStatus)
    {
        var result = await _orderService.UpdateOrderStatus(orderId, newStatus);
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
    
    [HttpPost("ready-for-pickup/{orderNumber}")]
    [Authorize(Roles = "Staff, Cashier")]
    public async Task<IActionResult> MarkOrderReady(int orderNumber)
    {
        await _orderService.EnsureOrderInStaffRestaurantAsync(orderNumber);

        string textInput =
            $"Xin mời khách hàng có số thứ tự {orderNumber} đến quầy nhận món.";

        string audioUrl = await _storageService.GetOrGenerateOrderAudioAsync(orderNumber, textInput);

        return Ok(new
        {
            Success = true,
            OrderNumber = orderNumber,
            AudioUrl = audioUrl 
        });
    }

    [HttpPost("scan-qr")]
    public async Task<ActionResult<ApiResponse<bool>>> ValidateQrCode([FromBody] ScanQrRequest request)
    {
        var isValid = await _orderService.ValidateQrCodeAsync(request.QrContent);
        return Success(isValid);
    }

    [HttpGet("customer/orders")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<CustomerOrderSummaryDto>>>> GetCustomerOrders(
        [FromQuery] int restaurantId,
        [FromQuery] string phone,
        [FromQuery] int limit = 20)
    {
        var result = await _orderService.GetCustomerOrdersAsync(restaurantId, phone, limit);
        return Success(result);
    }
}

