using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.Interfaces;
using StackExchange.Redis;

namespace ScanToOrder.Infrastructure.Services;

public class TransactionRedisService : ITransactionRedisService
{
    private readonly IDatabase _database;
    private readonly string _instanceName;

    public TransactionRedisService(IConnectionMultiplexer redis, IConfiguration config)
    {
        _database = redis.GetDatabase();
        _instanceName = config["RedisSettings:InstanceName"] ?? "";
    }

    private string GetKey(string transactionCode)
        => $"{_instanceName}transaction:{transactionCode}";

    private string GetOrderPaymentKey(string paymentCode)
        => $"{_instanceName}orderpayment:{paymentCode}";

    public async Task SaveTransactionCodeAsync(string transactionCode, Guid tenantId)
    {
        var key = GetKey(transactionCode);
        await _database.StringSetAsync(key, tenantId.ToString(), TimeSpan.FromMinutes(10));
    }

    public async Task<string?> GetTenantIdByTransactionCodeAsync(string transactionCode)
    {
        var key = GetKey(transactionCode);
        return await _database.StringGetAsync(key);
    }

    public async Task DeleteTransactionCodeAsync(string transactionCode)
    {
        var key = GetKey(transactionCode);
        await _database.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsTransactionCodeAsync(string transactionCode)
    {
        var key = GetKey(transactionCode);
        return await _database.KeyExistsAsync(key);
    }

    public async Task SaveOrderPaymentCodeAsync(string paymentCode, string cartId, TimeSpan? expiry = null)
    {
        var key = GetOrderPaymentKey(paymentCode);
        await _database.StringSetAsync(key, cartId, expiry ?? TimeSpan.FromMinutes(15));
    }

    public async Task<string?> GetCartIdByOrderPaymentCodeAsync(string paymentCode)
    {
        var key = GetOrderPaymentKey(paymentCode);
        return await _database.StringGetAsync(key);
    }

    public async Task DeleteOrderPaymentCodeAsync(string paymentCode)
    {
        var key = GetOrderPaymentKey(paymentCode);
        await _database.KeyDeleteAsync(key);
    }
}