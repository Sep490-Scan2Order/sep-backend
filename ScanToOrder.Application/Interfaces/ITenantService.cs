using ScanToOrder.Application.DTOs.User;

namespace ScanToOrder.Application.Interfaces
{
    public interface ITenantService
    {
        Task<string> RegisterTenantAsync(RegisterTenantRequest request);
        Task<IEnumerable<TenantDto>> GetAllTenantsAsync();
        Task<bool> BlockTenantAsync(Guid tenantId);
    }
}
