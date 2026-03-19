using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class RestaurantMenuService : IRestaurantMenuService
    {
        private readonly IRestaurantService _restaurantService;

        public RestaurantMenuService(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public Task<List<MenuCategoryDto>> GetMenuForRestaurantAsync(int restaurantId)
        {
            return _restaurantService.GetRestaurantMenuAsync(restaurantId);
        }
        
        public Task<List<MenuCategoryDto>> GetAllMenuForRestaurantAsync(int restaurantId)
        {
            return _restaurantService.GetRestaurantMenuAsync(restaurantId, false);
        }
    }
}

