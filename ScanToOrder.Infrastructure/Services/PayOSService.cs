using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using ScanToOrder.Application.DTOs.Payment;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Configuration;

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
        var d = request.Data;
        Console.WriteLine("------- Webhook Data Payload -------");
        Console.WriteLine($"OrderCode: {d.OrderCode}");
        Console.WriteLine($"Amount: {d.Amount}");
        Console.WriteLine($"Description: {d.Description}");
        Console.WriteLine($"AccountNumber (Của bạn): {d.AccountNumber}");
        Console.WriteLine($"Reference: {d.Reference}");
        Console.WriteLine($"TransactionDateTime: {d.TransactionDateTime}");
        Console.WriteLine($"Currency: {d.Currency}");
        Console.WriteLine($"PaymentLinkId: {d.PaymentLinkId}");
        Console.WriteLine($"Data-specific Code: {d.Code}");
        Console.WriteLine($"Data-specific Desc: {d.Description2}");

        Console.WriteLine("------- Counter Account Info -------");
        Console.WriteLine($"Counter Bank ID: {d.CounterAccountBankId ?? "null"}");
        Console.WriteLine($"Counter Bank Name: {d.CounterAccountBankName ?? "null"}");
        Console.WriteLine($"Counter Account Name: {d.CounterAccountName ?? "null"}");
        Console.WriteLine($"Counter Account Number: {d.CounterAccountNumber ?? "null"}");

        Console.WriteLine("------- Virtual Account Info -------");
        Console.WriteLine($"Virtual Account Name: {d.VirtualAccountName ?? "null"}");
        Console.WriteLine($"Virtual Account Number: {d.VirtualAccountNumber ?? "null"}");
        Console.WriteLine("======= [PAYOS WEBHOOK DEBUG END] =======");
        return new PaymentResult()
        {
            OrderCode = result.OrderCode,
            Description = result.Description,
            Amount = result.Amount,
            CounterAccountName = result.CounterAccountName,
            Reference = result.Reference,
            IsPaymentSuccess = request.Success && result.Code == "00"
        };
    }
}