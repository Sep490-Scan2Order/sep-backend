using System;
using System.Threading.Tasks;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.Interfaces;

public interface IOrderService
{
    Task<CartDto> AddToCartAsync(AddToCartRequest request);
    Task<CartDto> GetCartAsync(string cartId);
    Task<PaymentQrDto> GetPaymentQrAsync(string cartId, string phone, bool isPreOrder, DateTime? requestedPickupAt);
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

    Task<bool> ValidateQrCodeAsync(string qrContent);
    Task CancelExpiredUnpaidOrdersAsync();
    Task<bool> ConfirmPickupTimeAsync(ConfirmPickupTimeRequest request);
}

