using ScanToOrder.Domain.Entities.Restaurants;
using System.Linq.Expressions;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IRestaurantRepository : IGenericRepository<Restaurant>
    {
        Task<List<(Restaurant Restaurant, double DistanceKm)>> GetNearbyRestaurantsAsync(
            double latitude,
            double longitude,
            double radiusKm,
            int limit = 10);

        Task<(List<(Restaurant Restaurant, double DistanceKm)> Items, int TotalCount)> GetRestaurantsSortedByDistancePagedAsync(
            double latitude,
            double longitude,
            int page,
            int pageSize,
            string? keyword = null);

        Task<(List<Restaurant> Items, int TotalCount)> GetRestaurantsSortedByTotalOrderPagedAsync(int page, int pageSize, string? keyword = null);
        Task<List<Restaurant>> GetByTenantIdAsync(Guid tenantId);
        Task<Restaurant?> GetByIdIncludeSubscriptionAsync(int id);
        Task<Dictionary<int, Restaurant>> GetByIdsWithTenantId (List<int> ids, Guid tenantId);
        Task<Restaurant?> GetByIdWithTenantBankAsync(int restaurantId);
        Task<List<Restaurant>> GetRestaurantsWithSubscriptionsByTenantIdAsync(Guid tenantId);
    }
}
