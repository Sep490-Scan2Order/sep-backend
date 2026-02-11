using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.Interfaces
{
    public interface IRestaurantService
    {
        Task<List<RestaurantDto>> GetAllRestaurantsAsync();
    }
}
