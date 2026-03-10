using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IPlanRepository : IGenericRepository<Plan>
    {
        Task<Dictionary<int, Plan>> GetByIds (List<int> ids);
    }
}
