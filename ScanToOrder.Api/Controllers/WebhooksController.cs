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
    private readonly ITenantWalletService _tenantWalletService;
    private readonly ITenantService _tenantService;
    public WebhooksController(ITenantWalletService tenantWalletService, ITenantService tenantService)
    {
        _tenantWalletService = tenantWalletService;
        _tenantService = tenantService;
    }

    [HttpPost("payos")]
    [AllowAnonymous]
    public async Task<IActionResult> HandlePayOSWebhook([FromBody] Webhook webhookBody)
    {
        var result = await _tenantWalletService.HandleDepositWebhookAsync(webhookBody);
        
        if (result)
        {
            return Ok(new { Message = "Success" });
        }

        return BadRequest("Webhook processing failed");
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