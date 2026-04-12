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
        var result= await _payOSClient.Webhooks.VerifyAsync(request);
        _payOSClient.PaymentRequests.GetAsync(result.OrderCode); 
        var d = request.Data;
        return new PaymentResult()
        {
            OrderCode = result.OrderCode,
            Description = result.Description,
            Amount = result.Amount,
            CounterAccountName = result.CounterAccountName,
            Reference = result.Reference,
            IsPaymentSuccess = request.Success && result.Code == "00",
            BankBin = d.CounterAccountBankId,
            AccountNumber = d.CounterAccountNumber
        };
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
}