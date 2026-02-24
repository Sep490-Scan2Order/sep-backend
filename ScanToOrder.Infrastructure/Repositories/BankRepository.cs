using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Bank;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories;

public class BankRepository : GenericRepository<Banks>, IBankRepository
{
    private readonly AppDbContext _context;

    public BankRepository(AppDbContext context) : base(context)
    {
        _context = context;
    }
    
    public async Task<Banks?> GetByBinAsync(int bin)
    {
        return await _context.Banks.FirstOrDefaultAsync(b => b.Bin == bin);
    }
}