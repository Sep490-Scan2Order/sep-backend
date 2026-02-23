using ScanToOrder.Application.DTOs.External;

namespace ScanToOrder.Application.Interfaces
{
    public interface ITaxService
    {
        Task<bool> IsTaxCodeValidAsync(string taxCode);
        Task<TaxLookupResult> GetTaxCodeDetailsAsync(string taxCode);
    }
}
