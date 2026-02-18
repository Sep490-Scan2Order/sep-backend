using ScanToOrder.Application.DTOs.User;

namespace ScanToOrder.Application.Interfaces
{
    public interface ITenantService
    {
        Task<TenantDto> RegisterTenantAsync(RegisterTenantRequest request);
    }
}
