using ScanToOrder.Application.DTOs.User;

namespace ScanToOrder.Application.Interfaces
{
    public interface ITenantService
    {
        Task<bool> BlockTenantAsync(Guid id);
        Task<IEnumerable<TenantDto>> GetAllTenantsAsync();
        Task<string> RegisterTenantAsync(RegisterTenantRequest request);
    }
}
