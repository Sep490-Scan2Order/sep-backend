using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Domain.Interfaces
{
    public interface ICategoryRepository : IGenericRepository<Category>
    {
        Task<List<Category>> GetAllCategoriesByTenant(Guid tenantId);

        Task<int> GetTotalCategoriesByTenant(Guid tenantId);
    }
}
