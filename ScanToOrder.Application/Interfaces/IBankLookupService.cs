using ScanToOrder.Application.DTOs.External;

namespace ScanToOrder.Application.Interfaces;

public interface IBankLookupService
{
    Task<BankLookResponse> LookupAccountAsync(BankLookRequest request);
}