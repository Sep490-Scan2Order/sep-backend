using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IBranchDishConfigRepository : IGenericRepository<BranchDishConfig>
    {
        Task<List<BranchDishConfig>> GetByRestaurantIdWithIncludeAsync(int restaurantId);
        Task<BranchDishConfig?> GetByIdWithIncludeAsync(int id);
        Task<List<BranchDishConfig>> GetSellingDishesAsync(int restaurantId);
        Task AddRangeAsync(List<BranchDishConfig> configs);

        Task<List<BranchDishConfig>> GetConfigsByDishIdsAsync(List<int> dishIds);
        Task<bool> ReserveDishAvailabilityAsync(int restaurantId, int dishId, int quantity);
    }
}
