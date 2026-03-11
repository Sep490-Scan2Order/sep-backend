namespace ScanToOrder.Application.Interfaces;

public interface ITransactionRedisService
{
    Task SaveTransactionCodeAsync(string transactionCode, Guid tenantId);
    Task<string?> GetTenantIdByTransactionCodeAsync(string transactionCode);
    Task DeleteTransactionCodeAsync(string transactionCode);
    Task<bool> ExistsTransactionCodeAsync(string transactionCode);

    Task SaveOrderPaymentCodeAsync(string paymentCode, string cartId, TimeSpan? expiry = null);
    Task<string?> GetCartIdByOrderPaymentCodeAsync(string paymentCode);
    Task DeleteOrderPaymentCodeAsync(string paymentCode);
}