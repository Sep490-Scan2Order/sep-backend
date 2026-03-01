using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IDishesRepository : IGenericRepository<Dish>
    {
        Task<List<Dish>> GetAllDishesByTenant(Guid tenantId);

        Task<int> GetTotalDishesByTenant(Guid tenantId);
    }
}
