using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.Interfaces
{
    public interface IRestaurantService
    {
        Task<RestaurantDto?> GetRestaurantByIdAsync(int id);
        Task<List<RestaurantDto>> GetAllRestaurantsAsync();
        Task<PagedRestaurantResultDto> GetRestaurantsSortedByDistancePagedAsync(double latitude, double longitude, int page = 1, int pageSize = 20);
        Task<List<RestaurantDto>> GetNearbyRestaurantsAsync(double latitude, double longitude, double radiusKm, int limit = 10);
    }
}
