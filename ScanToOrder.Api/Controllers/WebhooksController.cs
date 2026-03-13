using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayOS;
using PayOS.Models.Webhooks;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Utils;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Api.Controllers;

public class WebhooksController : BaseController
{
    private readonly ITenantService _tenantService;
    private readonly IPaymentService _paymentService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IOrderService _orderService;

    public WebhooksController(
        ITenantService tenantService,
        ISubscriptionService subscriptionService,
        IPaymentService paymentService,
        IOrderService orderService)
    {
        _tenantService = tenantService;
        _subscriptionService = subscriptionService;
        _paymentService = paymentService;
        _orderService = orderService;
    }

    [HttpPost("payos")]
    [AllowAnonymous]
    public async Task<IActionResult> HandlePayOSWebhook([FromBody] Webhook webhookBody)
    {
        if (webhookBody.Data.OrderCode == 123)
        {
            return Ok(new { success = true });
        }
        try
        {
            var data = await _paymentService.VerifyWebhookAsync(webhookBody);
            if (data.IsPaymentSuccess)
            {
                await _subscriptionService.ProcessPaymentSuccessAsync(data.OrderCode);
            }
            else
            {
                await _subscriptionService.MarkPaymentFailedAsync(data.OrderCode);
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("sepay")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleSepayWebhook([FromBody] SePayWebhookDto webhookBody)
    {
        try
        {
            var paymentCode = webhookBody.Code
                              ?? ExtractPaymentCodeFromText(webhookBody.Content, webhookBody.Description);

            if (!string.IsNullOrWhiteSpace(paymentCode))
            {
                var result = BankQrLinkUtils.DetectPaymentIntent(paymentCode);
                if (result == PaymentIntent.TenantVerification)
                {
                    await _tenantService.VerifyBankAccountAsync(paymentCode, webhookBody.Gateway, webhookBody.AccountNumber);
                }
                else if (result == PaymentIntent.OrderPayment)
                {
                    await _orderService.ProcessOrderPaymentAsync(paymentCode, webhookBody.TransferAmount);
                }
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private static string? ExtractPaymentCodeFromText(params string?[] sources)
    {
        foreach (var src in sources)
        {
            if (string.IsNullOrWhiteSpace(src)) continue;

            var idx = src.IndexOf("SToO", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var span = src.AsSpan(idx);
            int end = 0;
            while (end < span.Length && !char.IsWhiteSpace(span[end]))
            {
                end++;
            }

            var candidate = span[..end].ToString();
            if (candidate.EndsWith("ORD", StringComparison.OrdinalIgnoreCase) ||
                candidate.EndsWith("VER", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
}