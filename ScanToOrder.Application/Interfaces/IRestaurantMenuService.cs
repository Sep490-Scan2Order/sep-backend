using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.Interfaces
{
    public interface IRestaurantMenuService
    {
        Task<List<MenuCategoryDto>> GetMenuForRestaurantAsync(int restaurantId);
        Task<List<MenuCategoryDto>> GetAllMenuForRestaurantAsync(int restaurantId);
    }
}

