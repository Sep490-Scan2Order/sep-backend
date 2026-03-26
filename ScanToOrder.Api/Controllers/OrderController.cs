using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Application.DTOs.Promotion;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Application.Message;

namespace ScanToOrder.Api.Controllers;

public class OrderController : BaseController
{
    private readonly IOrderService _orderService;
    private readonly IStorageService _storageService;
    private readonly IPromotionService _promotionService;
    private readonly IRestaurantService _restaurantService;

    public OrderController(
        IOrderService orderService, 
        IStorageService storageService,
        IPromotionService promotionService,
        IRestaurantService restaurantService)
    {
        _orderService = orderService;
        _storageService = storageService;
        _promotionService = promotionService;
        _restaurantService = restaurantService;
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
        var result = await _orderService.GetPaymentQrAsync(
            request.CartId,
            request.Phone,
            request.IsPreOrder,
            request.RequestedPickupAt,
            request.AppliedPromotionId);
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
    public async Task<ActionResult<ApiResponse<string>>> ValidateQrCode([FromBody] ScanQrRequest request)
    {
        var isValid = await _orderService.ValidateQrCodeAsync(request.QrContent, request.OrderNumber);
        return Success(isValid);
    }

    [HttpGet("customer/orders/active")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<CustomerOrderSummaryDto>>>> GetCustomerActiveOrders(
        [FromQuery] int restaurantId,
        [FromQuery] string phone)
    {
        var result = await _orderService.GetCustomerActiveOrdersAsync(restaurantId, phone);
        return Success(result);
    }

    [HttpGet("customer/orders/active/all-restaurants")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<CustomerOrderSummaryDto>>>> GetCustomerActiveOrdersAllRestaurants(
        [FromQuery] string phone)
    {
        var result = await _orderService.GetCustomerActiveOrdersAllRestaurantsAsync(phone);
        return Success(result);
    }

    [HttpPut("confirm-pickup-time")]
    [Authorize(Roles = "Staff, Cashier")]
    public async Task<ActionResult<ApiResponse<bool>>> ConfirmPickupTime([FromBody] ConfirmPickupTimeRequest request)
    {
        var result = await _orderService.ConfirmPickupTimeAsync(request);
        return Success(result, "Xác nhận thời gian nhận hàng thành công.");
    }

    [HttpPost("available-promotions")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<PromotionResponseDto>>>> GetAvailablePromotions([FromBody] GetAvailablePromotionsRequest request)
    {
        var restaurant = await _restaurantService.GetRestaurantByIdAsync(request.RestaurantId);
        if (restaurant == null) throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
        
        var result = await _promotionService.GetAvailablePromotionsByOrderAsync(restaurant.TenantId, request.RestaurantId, request.OrderTotal);
        return Success(result);
    }
}

