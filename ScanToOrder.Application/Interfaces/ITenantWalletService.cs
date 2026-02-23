using ScanToOrder.Application.DTOs.Wallet;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.Interfaces;

public interface ITenantWalletService
{
    Task<string> CreateDepositUrlAsync(decimal amount, NoteWalletTransaction note);
    Task<bool> HandleDepositWebhookAsync(object rawWebhook);
    Task<TenantWalletDto> CreateWalletTenantAsync(Guid tenantId);
}