using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Enums;

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
    public async Task<ActionResult<string>> WalletDeposit([FromQuery] decimal amount)
    {
        var depositUrl = await _tenantWalletService.CreateDepositUrlAsync(amount, NoteWalletTransaction.Deposit);
        return Ok(depositUrl);
    }
    
    [HttpPost("deposit-verify-tax")]
    public async Task<ActionResult<string>> WalletDepositVerifyTax()
    {
        var depositUrl = await _tenantWalletService.CreateDepositUrlAsync(5000, NoteWalletTransaction.AccountVerification);
        return Ok(depositUrl);
    }
}