using ScanToOrder.Domain.Entities.Bank;

namespace ScanToOrder.Domain.Interfaces;

public interface IBankRepository : IGenericRepository<Banks>
{
    Task<Banks?> GetByBinAsync(int bin);
}