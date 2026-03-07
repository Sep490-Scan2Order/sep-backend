using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Bank;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories;

public class BankRepository : GenericRepository<Banks>, IBankRepository
{
    public BankRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Banks?> GetByBinAsync(int bin)
    {
        return await _dbSet.FirstOrDefaultAsync(b => b.Bin == bin);
    }
}