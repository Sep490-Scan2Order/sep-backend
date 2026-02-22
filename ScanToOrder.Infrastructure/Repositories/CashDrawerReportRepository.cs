using ScanToOrder.Domain.Entities.CashReport;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class CashDrawerReportRepository : GenericRepository<CashDrawerReport>, ICashDrawerReportRepository
    {
        private readonly AppDbContext _context;
        public CashDrawerReportRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
