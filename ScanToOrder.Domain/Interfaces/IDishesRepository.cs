using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IDishesRepository : IGenericRepository<Dish>
    {
        Task<List<Dish>> GetAllDishesByTenant(Guid tenantId, bool includeDeleted = false);

        Task<int> GetTotalDishesByTenant(Guid tenantId);

        Task<List<Dish>> GetDishesByCategoryIdAsync(int categoryId);
    }
}
