using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using StackExchange.Redis;

namespace ScanToOrder.Infrastructure.Services;

public class MenuCacheService : IMenuCacheService
{
    private readonly IDatabase _database;
    private readonly string _instanceName;
    private readonly ILogger<MenuCacheService> _logger;

    // Default TTL: 5 minutes - short enough to reflect promo changes, long enough to matter
    private static readonly TimeSpan DefaultMenuTtl = TimeSpan.FromMinutes(5);

    public MenuCacheService(IConnectionMultiplexer redis, IConfiguration config, ILogger<MenuCacheService> logger)
    {
        _database = redis.GetDatabase();
        _instanceName = config["RedisSettings:InstanceName"] ?? "";
        _logger = logger;
    }

    private string GetMenuKey(int restaurantId)
        => $"{_instanceName}menu:{restaurantId}";

    public async Task<List<MenuCategoryDto>?> GetMenuAsync(int restaurantId)
    {
        try
        {
            var key = GetMenuKey(restaurantId);
            var cached = await _database.StringGetAsync(key);

            if (!cached.HasValue)
                return null;

            return JsonSerializer.Deserialize<List<MenuCategoryDto>>(cached!);
        }
        catch (Exception ex)
        {
            // Swallow Redis errors — always fall through to DB
            _logger.LogWarning(ex, "Redis GetMenuAsync failed for restaurantId={RestaurantId}. Falling back to DB.", restaurantId);
            return null;
        }
    }

    public async Task SetMenuAsync(int restaurantId, List<MenuCategoryDto> menu, TimeSpan? expiry = null)
    {
        try
        {
            var key = GetMenuKey(restaurantId);
            var json = JsonSerializer.Serialize(menu);
            await _database.StringSetAsync(key, json, expiry ?? DefaultMenuTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SetMenuAsync failed for restaurantId={RestaurantId}. Cache write skipped.", restaurantId);
        }
    }

    public async Task InvalidateMenuAsync(int restaurantId)
    {
        try
        {
            var key = GetMenuKey(restaurantId);
            await _database.KeyDeleteAsync(key);
            _logger.LogInformation("Menu cache invalidated for restaurantId={RestaurantId}", restaurantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis InvalidateMenuAsync failed for restaurantId={RestaurantId}.", restaurantId);
        }
    }

    public async Task UpdateMenuStockInCacheAsync(int restaurantId, IEnumerable<(int DishId, int Quantity)> reservedItems)
    {
        try
        {
            var menu = await GetMenuAsync(restaurantId);
            if (menu == null || !menu.Any()) return;

            bool isUpdated = false;

            foreach (var category in menu)
            {
                foreach (var dish in category.Dishes)
                {
                    var reserved = reservedItems.FirstOrDefault(r => r.DishId == dish.DishId);
                    if (reserved.Quantity > 0)
                    {
                        dish.DishAvailabilityStock -= reserved.Quantity;
                        if (dish.DishAvailabilityStock <= 0)
                        {
                            dish.DishAvailabilityStock = 0;
                            dish.IsSoldOut = true;
                        }
                        isUpdated = true;
                    }
                }
            }

            if (isUpdated)
            {
                var key = GetMenuKey(restaurantId);
                var expiry = await _database.KeyTimeToLiveAsync(key);
                await SetMenuAsync(restaurantId, menu, expiry ?? DefaultMenuTtl);
                _logger.LogInformation("Menu cache patched successfully for restaurantId={RestaurantId}", restaurantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis UpdateMenuStockInCacheAsync failed for restaurantId={RestaurantId}.", restaurantId);
        }
    }

    public async Task SetDishStockInCacheAsync(int restaurantId, int dishId, bool isSoldOut, int quantity)
    {
        try
        {
            var menu = await GetMenuAsync(restaurantId);
            if (menu == null || !menu.Any()) return;

            bool isUpdated = false;

            foreach (var category in menu)
            {
                foreach (var dish in category.Dishes)
                {
                    if (dish.DishId == dishId)
                    {
                        dish.IsSoldOut = isSoldOut;
                        dish.DishAvailabilityStock = isSoldOut ? 0 : quantity;
                        isUpdated = true;
                        break;
                    }
                }
                if (isUpdated) break;
            }

            if (isUpdated)
            {
                var key = GetMenuKey(restaurantId);
                var expiry = await _database.KeyTimeToLiveAsync(key);
                await SetMenuAsync(restaurantId, menu, expiry ?? DefaultMenuTtl);
                _logger.LogInformation("Menu cache patched successfully (SetDishStock) for restaurantId={RestaurantId}, dishId={DishId}", restaurantId, dishId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SetDishStockInCacheAsync failed for restaurantId={RestaurantId}, dishId={DishId}.", restaurantId, dishId);
        }
    }
}
