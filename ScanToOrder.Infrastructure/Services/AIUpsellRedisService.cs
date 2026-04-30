using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.Interfaces;
using StackExchange.Redis;

namespace ScanToOrder.Infrastructure.Services
{
    public class AIUpsellRedisService : IAIUpsellRedisService
    {
        private readonly IDatabase _database;
        private readonly string _instanceName;

        public AIUpsellRedisService(IConnectionMultiplexer redis, IConfiguration config)
        {
            _database = redis.GetDatabase();
            _instanceName = config["RedisSettings:InstanceName"] ?? "";
        }

        private string GetEligibilityKey(int restaurantId) => $"{_instanceName}upsell:eligibility:{restaurantId}";
        private string GetBestSellersKey(int restaurantId) => $"{_instanceName}upsell:bestsellers:{restaurantId}";

        public async Task SetAIEligibilityAsync(int restaurantId, bool isEligible)
        {
            var key = GetEligibilityKey(restaurantId);
            await _database.StringSetAsync(key, isEligible.ToString(), TimeSpan.FromHours(2));
        }

        public async Task<bool> GetAIEligibilityAsync(int restaurantId)
        {
            var key = GetEligibilityKey(restaurantId);
            var value = await _database.StringGetAsync(key);
            if (value.IsNullOrEmpty) return false;
            
            return bool.TryParse(value.ToString(), out bool isEligible) && isEligible;
        }

        public async Task SetBestSellersAsync(int restaurantId, List<int> dishIds)
        {
            var key = GetBestSellersKey(restaurantId);
            var json = JsonSerializer.Serialize(dishIds);
            await _database.StringSetAsync(key, json, TimeSpan.FromHours(2));
        }

        public async Task<List<int>> GetBestSellersAsync(int restaurantId)
        {
            var key = GetBestSellersKey(restaurantId);
            var value = await _database.StringGetAsync(key);
            if (value.IsNullOrEmpty) return new List<int>();

            try
            {
                return JsonSerializer.Deserialize<List<int>>(value.ToString()!) ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }
    }
}
