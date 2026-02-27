using ScanToOrder.Application.DTOs.User;

namespace ScanToOrder.Application.Interfaces
{
    public interface ITenantService
    {
        Task<string> RegisterTenantAsync(RegisterTenantRequest request);
        Task<IEnumerable<TenantDto>> GetAllTenantsAsync();
        Task<string> UpdateTenantAsync(UpdateTenantDtoRequest updateTenantDtoRequest);
        Task<bool> UpdateTenantStatusAsync(Guid tenantId, bool isActive);
        Task<bool> ValidationTaxCodeAsync(string taxCode);
        Task<string> UpdateBankInfoAsync(Guid bankId, string accountNumber);
        Task<bool> VerifyBankAccountAsync(string paymentCode);
    }
}
