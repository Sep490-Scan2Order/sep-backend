using ScanToOrder.Domain.Entities.Restaurant;

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
            int pageSize);

        Task<(List<Restaurant> Items, int TotalCount)> GetRestaurantsSortedByTotalOrderPagedAsync(int page, int pageSize);
    }
}
