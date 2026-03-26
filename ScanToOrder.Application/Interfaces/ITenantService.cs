using ScanToOrder.Application.DTOs.Orders;
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
        Task<bool> VerifyBankAccountAsync(string paymentCode, string gateway, string accountNumber);
        Task<TenantDto> GetTenantByIdAsync(Guid tenantId);

        Task<TotalRevenueByTenantDto> GetTotalRevenueByTenantAsync(
            Guid? tenantId,
            DateTime? startDate,
            DateTime? endDate,
            string? preset);
    }
}
