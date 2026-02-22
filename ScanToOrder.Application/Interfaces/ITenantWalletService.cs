namespace ScanToOrder.Application.Interfaces;

public interface ITenantWalletService
{
    Task<string> CreateDepositUrlAsync(decimal amount);
    Task<bool> HandleDepositWebhookAsync(object rawWebhook);
}