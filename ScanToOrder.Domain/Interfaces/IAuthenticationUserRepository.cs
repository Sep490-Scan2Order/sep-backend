using ScanToOrder.Domain.Entities.Authentication;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IAuthenticationUserRepository : IGenericRepository<AuthenticationUser>
    {
        Task<AuthenticationUser?> GetByPhoneAsync(string phone);
        Task<AuthenticationUser?> GetByEmailAsync(string email);
    }
}

