using System.Threading.Tasks;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.Interfaces;

public interface IOrderService
{
    Task<CartDto> AddToCartAsync(AddToCartRequest request);
    Task<CartDto> GetCartAsync(string cartId);
    Task<PaymentQrDto> GetPaymentQrAsync(string cartId);
    Task ProcessOrderPaymentAsync(string paymentCode, decimal transferAmount);
    Task<List<KdsOrderResponse>> GetKdsActiveOrders(int restaurantId);

    Task<bool> UpdateOrderStatus(Guid orderId, OrderStatus newStatus);
}

