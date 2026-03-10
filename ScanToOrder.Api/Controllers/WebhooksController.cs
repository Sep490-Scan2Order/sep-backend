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

    public WebhooksController(ITenantService tenantService, ISubscriptionService subscriptionService, IPaymentService paymentService)
    {
        _tenantService = tenantService;
        _subscriptionService = subscriptionService;
        _paymentService = paymentService;
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
        if (webhookBody.Code != null)
        {
            var result = BankQrLinkUtils.DetectPaymentIntent(webhookBody.Code);
            if (result == PaymentIntent.TenantVerification)
            {
                await _tenantService.VerifyBankAccountAsync(webhookBody.Code);
            }
        }

        return Ok();
    }
}