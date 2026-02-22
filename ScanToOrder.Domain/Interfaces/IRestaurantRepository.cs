using ScanToOrder.Domain.Entities.Restaurants;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IRestaurantRepository : IGenericRepository<Restaurant>
    {
        Task<List<(Restaurant Restaurant, double DistanceKm)>> GetNearbyRestaurantsAsync(
            double latitude,
            double longitude,
            double radiusKm,
            int limit = 10);
    }
}
