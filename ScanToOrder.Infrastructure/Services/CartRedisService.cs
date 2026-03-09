using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.Interfaces;
using StackExchange.Redis;

namespace ScanToOrder.Infrastructure.Services;

public class CartRedisService : ICartRedisService
{
    private readonly IDatabase _database;
    private readonly string _instanceName;

    public CartRedisService(IConnectionMultiplexer redis, IConfiguration config)
    {
        _database = redis.GetDatabase();
        _instanceName = config["RedisSettings:InstanceName"] ?? "";
    }

    private string GetKey(string cartId)
        => $"{_instanceName}cart:{cartId}";

    public async Task<string?> GetRawCartAsync(string cartId)
    {
        var key = GetKey(cartId);
        return await _database.StringGetAsync(key);
    }

    public async Task SaveRawCartAsync(string cartId, string json, TimeSpan? expiry = null)
    {
        var key = GetKey(cartId);
        var ttl = expiry ?? TimeSpan.FromMinutes(60);
        await _database.StringSetAsync(key, json, ttl);
    }

    public async Task DeleteCartAsync(string cartId)
    {
        var key = GetKey(cartId);
        await _database.KeyDeleteAsync(key);
    }
}

