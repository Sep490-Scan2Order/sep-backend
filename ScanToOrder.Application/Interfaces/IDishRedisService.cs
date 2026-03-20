using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces;

public interface IDishRedisService
{
    Task SetDishSellingStatusAsync(int restaurantId, int dishId, bool isSelling);
    Task<Dictionary<int, bool>> GetDishSellingStatusesAsync(int restaurantId);
    Task<IEnumerable<int>> GetAllRestaurantsWithUnsyncedSellingStatusesAsync();
    Task ClearSyncedSellingStatusesAsync(int restaurantId);

    Task SetDishPriceAsync(int restaurantId, int dishId, decimal price);
    Task<Dictionary<int, decimal>> GetDishPricesAsync(int restaurantId);
    Task<IEnumerable<int>> GetAllRestaurantsWithUnsyncedPricesAsync();
    Task ClearSyncedPricesAsync(int restaurantId);
}