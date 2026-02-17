using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Application.Interfaces
{
    public interface ITenantService
    {
        Task<ApiResponse<TenantDto>> RegisterTenantAsync(RegisterTenantRequest request);
    }
}
