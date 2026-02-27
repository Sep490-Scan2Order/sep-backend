using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.Interfaces
{
    public interface IRestaurantService
    {
        Task<RestaurantDto?> GetRestaurantByIdAsync(int id);
        Task<PagedRestaurantResultDto> GetRestaurantsPagedAsync(double? latitude, double? longitude, int page = 1, int pageSize = 20);
        Task<List<RestaurantDto>> GetNearbyRestaurantsAsync(double latitude, double longitude, double radiusKm, int limit = 10);

        Task<RestaurantDto> CreateRestaurantAsync(Guid tenantId, CreateRestaurantRequest request);
        Task<byte[]> GetRestaurantQrImageBySlugAsync(string slug);
        Task<RestaurantDto> GetRestaurantBySlugAsync(string slug);
        Task<IEnumerable<RestaurantDto>> GetRestaurantsByTenantIdAsync(Guid tenantId);
    }
}
