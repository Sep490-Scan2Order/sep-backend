using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IBranchDishConfigRepository : IGenericRepository<BranchDishConfig>
    {
        Task<List<BranchDishConfig>> GetByRestaurantIdWithIncludeAsync(int restaurantId);

        Task<BranchDishConfig?> GetByIdWithIncludeAsync(int id);
    }
}
