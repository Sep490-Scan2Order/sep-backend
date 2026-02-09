using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<AuthenticationUser> AuthenticationUsers { get; }
        IGenericRepository<Tenant> Tenants { get; }
        Task SaveAsync();
    }
}
