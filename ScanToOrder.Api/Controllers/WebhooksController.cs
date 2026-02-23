using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayOS;
using PayOS.Models.Webhooks;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Api.Controllers;

public class WebhooksController : BaseController
{
    private readonly ITenantWalletService _tenantWalletService;

    public WebhooksController(ITenantWalletService tenantWalletService)
    {
        _tenantWalletService = tenantWalletService;
    }

    [HttpPost("payos")]
    [AllowAnonymous]
    public async Task<IActionResult> HandlePayOSWebhook([FromBody] Webhook webhookBody)
    {
        if (webhookBody.Data.Description != "Nạp tiền vào ví chủ quán")
        {
            
        }
        var result = await _tenantWalletService.HandleDepositWebhookAsync(webhookBody);
        
        if (result)
        {
            return Ok(new { Message = "Success" });
        }

        return BadRequest("Webhook processing failed");
    }
}