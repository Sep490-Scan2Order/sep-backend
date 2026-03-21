using ScanToOrder.Application.DTOs.Restaurant;

namespace ScanToOrder.Application.Interfaces;

public interface IMenuCacheService
{
    Task<List<MenuCategoryDto>?> GetMenuAsync(int restaurantId);
    
    Task SetMenuAsync(int restaurantId, List<MenuCategoryDto> menu, TimeSpan? expiry = null);
    
    Task InvalidateMenuAsync(int restaurantId);
}
