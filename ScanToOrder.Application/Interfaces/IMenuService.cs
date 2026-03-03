using ScanToOrder.Application.DTOs.Menu;

namespace ScanToOrder.Application.Interfaces
{
    public interface IMenuService
    {
        Task<IEnumerable<MenuRestaurantDto>> GetMenuByRestaurantIdAsync(int tenantId);
    }
}
