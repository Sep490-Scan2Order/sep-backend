using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Api.Controllers;

[Authorize(Roles = "Tenant")]
public class TenantWalletController : BaseController
{
    private readonly ITenantWalletService _tenantWalletService;

    public TenantWalletController(ITenantWalletService tenantWalletService)
    {
        _tenantWalletService = tenantWalletService;
    }
    
    [HttpPost("deposit")]
    public async Task<ActionResult<string>> CreateDepositUrl([FromQuery] decimal amount)
    {
        var depositUrl = await _tenantWalletService.CreateDepositUrlAsync(amount);
        return Ok(depositUrl);
    }
}