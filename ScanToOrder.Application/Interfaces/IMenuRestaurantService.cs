using ScanToOrder.Application.DTOs.Menu;

namespace ScanToOrder.Application.Interfaces
{
    public interface IMenuRestaurantService
    {
        Task<MenuRestaurantDto> ApplyRestaurantWithTemplateAsync(CreateMenuRestaurantRequestDto createMenuRestaurantRequestDto);
        Task<MenuRestaurantDto> GetMenuByRestaurantIdAsync(int restaurantId);
    }
}
