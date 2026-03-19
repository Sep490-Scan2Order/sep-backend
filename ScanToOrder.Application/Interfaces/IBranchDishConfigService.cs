using ScanToOrder.Application.DTOs.Dishes;

namespace ScanToOrder.Application.Interfaces
{
    public interface IBranchDishConfigService
    {
        Task<BranchDishConfigDto> ConfigDishByRestaurant(CreateBranchDishConfig request);

        Task<List<BranchDishConfigDto>> GetBranchDishByRestaurant(int restaurantId);

        Task<BranchDishConfigDto> ToggleSoldOutAsync(int branchDishConfigId, bool isSoldOut);

        Task<string> UpdateIsSoldOutBranchDish(int restaurantId, int dishId, bool isSoldOut, int quantity);
        Task<string> UpdateIsSellingBranchDish(int restaurantId, int dishId, bool isSelling);
        Task<string> SyncDishesToBranchDishConfigAsync(Guid tenantId);
    }
}
