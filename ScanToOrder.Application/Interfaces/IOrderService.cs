using System.Threading.Tasks;
using ScanToOrder.Application.DTOs.Orders;

namespace ScanToOrder.Application.Interfaces;

public interface IOrderService
{
    Task<CartDto> AddToCartAsync(AddToCartRequest request);
    Task<CartDto> GetCartAsync(string cartId);
    Task<PaymentQrDto> GetPaymentQrAsync(string cartId);
    Task ProcessOrderPaymentAsync(string paymentCode, decimal transferAmount);
}

