using ScanToOrder.Application.DTOs.Wallet;

namespace ScanToOrder.Application.Interfaces;

public interface ITenantWalletService
{
    Task<string> CreateDepositUrlAsync(decimal amount);
    Task<bool> HandleDepositWebhookAsync(object rawWebhook);
    Task<TenantWalletDto> CreateWalletTenantAsync(Guid tenantId);
}