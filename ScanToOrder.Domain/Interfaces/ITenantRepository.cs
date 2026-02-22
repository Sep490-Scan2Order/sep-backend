using ScanToOrder.Domain.Entities.User;

namespace ScanToOrder.Domain.Interfaces
{
    public interface ITenantRepository : IGenericRepository<Tenant>
    {
        Task<List<Tenant>> GetTenantsWithSubscriptionsAsync();
    }
}
