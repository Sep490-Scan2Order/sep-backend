using ScanToOrder.Application.DTOs.Orders;

namespace ScanToOrder.Application.Interfaces
{
    public interface IRefundService
    {
        Task<bool> RefundOrderAsync(RefundRequest request);
        Task<bool> ConfirmSystemErrorPaymentAsync(ConfirmSystemPaymentRequest request);
    }
}
