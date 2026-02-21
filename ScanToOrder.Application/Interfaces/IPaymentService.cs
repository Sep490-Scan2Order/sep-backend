using ScanToOrder.Application.DTOs.Payment;

namespace ScanToOrder.Application.Interfaces;

public interface IPaymentService
{
    Task<string> CreatePaymentLinkAsync(CreatePaymentRequest request);
    Task<PaymentResult> VerifyWebhookAsync(object webhookRequest);
}