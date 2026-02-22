using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.Interfaces
{
    public interface IRestaurantService
    {
        Task<List<RestaurantDto>> GetAllRestaurantsAsync();
        Task<List<RestaurantDto>> GetNearbyRestaurantsAsync(double latitude, double longitude, double radiusKm, int limit = 10);
    }
}
