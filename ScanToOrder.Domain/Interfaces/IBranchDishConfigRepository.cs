using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IBranchDishConfigRepository : IGenericRepository<BranchDishConfig>
    {
        Task<List<BranchDishConfig>> GetByRestaurantIdWithIncludeAsync(int restaurantId);
        Task<BranchDishConfig?> GetByIdWithIncludeAsync(int id);
        Task AddRangeAsync(List<BranchDishConfig> configs);
        Task<List<BranchDishConfig>> GetSellingDishesByRestaurantIdAsync(int restaurantId);
        Task<bool> ReserveDishAvailabilityAsync(int restaurantId, int dishId, int quantity);
        Task<bool> RefundDishAvailabilityAsync(int restaurantId, int dishId, int quantity);
        Task<bool> RefundDishAvailabilityBatchAsync(int restaurantId, Dictionary<int, int> dishQuantities);
        Task<List<BranchDishConfig>> GetSellingDishesByRestaurantIdAndDishIdsAsync(int restaurantId, List<int> dishIds);
    }
}
