using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.Interfaces;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScanToOrder.Infrastructure.Services;

public class DishRedisService : IDishRedisService
{
    private readonly IDatabase _database;
    private readonly string _instanceName;

    // Keys used in Redis
    private const string UnsyncedRestaurantsSetKey = "UnsyncedRestaurants:DishSelling";
    private const string UnsyncedRestaurantsPriceSetKey = "UnsyncedRestaurants:DishPrice";

    public DishRedisService(IConnectionMultiplexer redis, IConfiguration config)
    {
        _database = redis.GetDatabase();
        _instanceName = config["RedisSettings:InstanceName"] ?? "";
    }

    private string GetBranchDishSellingKey(int restaurantId)
        => $"{_instanceName}BranchDishSelling:{restaurantId}";

    private string GetUnsyncedRestaurantsSetKey()
        => $"{_instanceName}{UnsyncedRestaurantsSetKey}";

    private string GetBranchDishPriceKey(int restaurantId)
        => $"{_instanceName}BranchDishPrice:{restaurantId}";

    private string GetUnsyncedRestaurantsPriceSetKey()
        => $"{_instanceName}{UnsyncedRestaurantsPriceSetKey}";

    public async Task SetDishSellingStatusAsync(int restaurantId, int dishId, bool isSelling)
    {
        var hashKey = GetBranchDishSellingKey(restaurantId);
        var setKey = GetUnsyncedRestaurantsSetKey();

        // 1. Update the hash value (1 for true, 0 for false)
        await _database.HashSetAsync(hashKey, dishId.ToString(), isSelling ? "1" : "0");

        // 2. Add restaurantId to the set of unsynced restaurants
        await _database.SetAddAsync(setKey, restaurantId.ToString());
    }

    public async Task<Dictionary<int, bool>> GetDishSellingStatusesAsync(int restaurantId)
    {
        var hashKey = GetBranchDishSellingKey(restaurantId);
        var hashEntries = await _database.HashGetAllAsync(hashKey);

        var result = new Dictionary<int, bool>();
        foreach (var entry in hashEntries)
        {
            if (int.TryParse(entry.Name, out int dishId))
            {
                result[dishId] = entry.Value == "1";
            }
        }
        return result;
    }

    public async Task<IEnumerable<int>> GetAllRestaurantsWithUnsyncedSellingStatusesAsync()
    {
        var setKey = GetUnsyncedRestaurantsSetKey();
        var members = await _database.SetMembersAsync(setKey);

        var result = new List<int>();
        foreach (var member in members)
        {
            if (int.TryParse(member, out int restaurantId))
            {
                result.Add(restaurantId);
            }
        }
        return result;
    }

    public async Task ClearSyncedSellingStatusesAsync(int restaurantId)
    {
        var hashKey = GetBranchDishSellingKey(restaurantId);
        var setKey = GetUnsyncedRestaurantsSetKey();

        // Remove the hash completely
        await _database.KeyDeleteAsync(hashKey);

        // Remove the restaurant from the unsynced tracking set
        await _database.SetRemoveAsync(setKey, restaurantId.ToString());
    }

    public async Task SetDishPriceAsync(int restaurantId, int dishId, decimal price)
    {
        var hashKey = GetBranchDishPriceKey(restaurantId);
        var setKey = GetUnsyncedRestaurantsPriceSetKey();

        await _database.HashSetAsync(hashKey, dishId.ToString(), price.ToString());
        await _database.SetAddAsync(setKey, restaurantId.ToString());
    }

    public async Task<Dictionary<int, decimal>> GetDishPricesAsync(int restaurantId)
    {
        var hashKey = GetBranchDishPriceKey(restaurantId);
        var hashEntries = await _database.HashGetAllAsync(hashKey);

        var result = new Dictionary<int, decimal>();
        foreach (var entry in hashEntries)
        {
            if (int.TryParse(entry.Name, out int dishId) && decimal.TryParse(entry.Value, out decimal price))
            {
                result[dishId] = price;
            }
        }
        return result;
    }

    public async Task<IEnumerable<int>> GetAllRestaurantsWithUnsyncedPricesAsync()
    {
        var setKey = GetUnsyncedRestaurantsPriceSetKey();
        var members = await _database.SetMembersAsync(setKey);

        var result = new List<int>();
        foreach (var member in members)
        {
            if (int.TryParse(member, out int restaurantId))
            {
                result.Add(restaurantId);
            }
        }
        return result;
    }

    public async Task ClearSyncedPricesAsync(int restaurantId)
    {
        var hashKey = GetBranchDishPriceKey(restaurantId);
        var setKey = GetUnsyncedRestaurantsPriceSetKey();

        await _database.KeyDeleteAsync(hashKey);
        await _database.SetRemoveAsync(setKey, restaurantId.ToString());
    }
}