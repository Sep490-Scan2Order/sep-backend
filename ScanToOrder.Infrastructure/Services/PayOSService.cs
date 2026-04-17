using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using ScanToOrder.Application.DTOs.Payment;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Configuration;
using System.Text.Json;

namespace ScanToOrder.Infrastructure.Services;

public class PayOSService : IPaymentService
{
    private readonly PayOSClient _payOSClient;
    private readonly PayOSSettings _payOsSettings;

    public PayOSService(PayOSClient payOsClient, IOptions<PayOSSettings> payOsOptions)
    {
        _payOSClient = payOsClient;
        _payOsSettings = payOsOptions.Value;
    }

    public async Task<string> CreatePaymentLinkAsync(CreatePaymentRequest request)
    {
        var paymentRequest  = new CreatePaymentLinkRequest
        {
            OrderCode = request.OrderCode,
            Amount = request.Amount,
            Description = request.Description,
            CancelUrl = request.CancelUrl,
            ReturnUrl = request.ReturnUrl,
        };
        var result = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);
        return result.CheckoutUrl;
    }

    public async Task<PaymentResult> VerifyWebhookAsync(object webhookRequest)
    {
        Webhook request = (Webhook)webhookRequest;

        // Dòng này đã được phủ (màu xanh) bởi các test ném lỗi Integrity hiện tại
        var result = await _payOSClient.Webhooks.VerifyAsync(request);

        // Dòng này đang màu đỏ vì VerifyAsync phía trên luôn throw
        var finalResult = MapToPaymentResult(request, result);
        return finalResult;
    }

    public async Task<bool> IsPaymentSuccessfulAsync(long orderCode)
    {
        var paymentInfo = await _payOSClient.PaymentRequests.GetAsync(orderCode);

        var json = JsonSerializer.Serialize(paymentInfo);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string status = TryGetString(root, "status") ?? string.Empty;
        string code = TryGetString(root, "code") ?? string.Empty;

        if (status.Equals("PAID", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (code == "00")
        {
            return true;
        }

        return false;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    public PaymentResult MapToPaymentResult(Webhook request, WebhookData verifiedResult)
    {
        return new PaymentResult()
        {
            OrderCode = verifiedResult.OrderCode,
            Description = verifiedResult.Description,
            Amount = verifiedResult.Amount,
            CounterAccountName = verifiedResult.CounterAccountName,
            Reference = verifiedResult.Reference,
            IsPaymentSuccess = request.Success && verifiedResult.Code == "00",
            BankBin = request.Data.CounterAccountBankId,
            AccountNumber = request.Data.CounterAccountNumber
        };
    }
}