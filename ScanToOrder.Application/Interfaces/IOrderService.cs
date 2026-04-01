using System;
using System.Threading.Tasks;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.DTOs.Other;

namespace ScanToOrder.Application.Interfaces;

public interface IOrderService
{
    Task<CartDto> AddToCartAsync(AddToCartRequest request);
    Task<CartDto> GetCartAsync(string cartId);
    Task<PaymentQrDto> GetPaymentQrAsync(string cartId, string phone, bool isPreOrder, DateTime? requestedPickupAt, int? appliedPromotionId);
    Task<CashCheckoutResponse> CheckoutCashAsync(CashCheckoutRequest request);
    Task ConfirmCashPaymentAsync(Guid orderId);
    Task<List<CashPendingOrderResponse>> GetCashOrdersPendingConfirmAsync();
    Task EnsureOrderInStaffRestaurantAsync(int orderNumber);
    Task ProcessOrderPaymentAsync(string paymentCode, decimal transferAmount);
    Task<List<MenuDishItemDto>> GetDishesByIdsWithPromotionAsync(int restaurantId, List<int> dishIds);
    Task<List<KdsOrderResponse>> GetKdsActiveOrders(int restaurantId);
    Task<List<CustomerOrderSummaryDto>> GetCustomerActiveOrdersAsync(int restaurantId, string phone);
    Task<List<CustomerOrderSummaryDto>> GetCustomerActiveOrdersAllRestaurantsAsync(string phone);
    Task<bool> UpdateOrderStatus(Guid orderId, OrderStatus newStatus);

    Task<string> ValidateQrCodeAsync(string qrContent, int orderNumber);
    Task CancelExpiredUnpaidOrdersAsync();
    Task<bool> ConfirmPickupTimeAsync(ConfirmPickupTimeRequest request);

    Task<PagedResult<TenantOrderResponseDto>> GetTenantOrdersAsync(
        int restaurantId,
        int pageIndex,
        int pageSize,
        string? keyword = null,
        OrderStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);
}

