using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using PayOS.Resources.V2.PaymentRequests;
using PayOS.Resources.Webhooks;

namespace ScanToOrder.Infrastructure.Services;

public interface IPayOSClientAdapter
{
    IPayOSPaymentRequestsAdapter PaymentRequests { get; }

    IPayOSWebhooksAdapter Webhooks { get; }
}

public interface IPayOSPaymentRequestsAdapter
{
    Task<IPayOSCreatePaymentLinkResponse> CreateAsync(CreatePaymentLinkRequest request);

    Task<object> GetAsync(long orderCode);
}

public interface IPayOSCreatePaymentLinkResponse
{
    string CheckoutUrl { get; }
}

public interface IPayOSWebhooksAdapter
{
    Task<WebhookData> VerifyAsync(Webhook request);
}

public sealed class PayOSClientAdapter : IPayOSClientAdapter
{
    public PayOSClientAdapter(PayOSClient client)
    {
        PaymentRequests = new PayOSPaymentRequestsAdapter(client.PaymentRequests);
        Webhooks = new PayOSWebhooksAdapter(client.Webhooks);
    }

    public IPayOSPaymentRequestsAdapter PaymentRequests { get; }

    public IPayOSWebhooksAdapter Webhooks { get; }
}

internal sealed class PayOSPaymentRequestsAdapter : IPayOSPaymentRequestsAdapter
{
    private readonly PaymentRequests _paymentRequests;

    public PayOSPaymentRequestsAdapter(PaymentRequests paymentRequests)
    {
        _paymentRequests = paymentRequests;
    }

    public async Task<IPayOSCreatePaymentLinkResponse> CreateAsync(CreatePaymentLinkRequest request)
    {
        var response = await _paymentRequests.CreateAsync(request);
        return new PayOSCreatePaymentLinkResponse(response.CheckoutUrl);
    }

    public async Task<object> GetAsync(long orderCode)
    {
        return await _paymentRequests.GetAsync(orderCode);
    }
}

internal sealed class PayOSCreatePaymentLinkResponse : IPayOSCreatePaymentLinkResponse
{
    public PayOSCreatePaymentLinkResponse(string checkoutUrl)
    {
        CheckoutUrl = checkoutUrl;
    }

    public string CheckoutUrl { get; }
}

internal sealed class PayOSWebhooksAdapter : IPayOSWebhooksAdapter
{
    private readonly Webhooks _webhooks;

    public PayOSWebhooksAdapter(Webhooks webhooks)
    {
        _webhooks = webhooks;
    }

    public Task<WebhookData> VerifyAsync(Webhook request)
    {
        return _webhooks.VerifyAsync(request);
    }
}