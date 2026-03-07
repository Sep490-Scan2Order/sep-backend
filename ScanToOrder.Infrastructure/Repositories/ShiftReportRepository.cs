using ScanToOrder.Domain.Entities.Shift;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class ShiftReportRepository : GenericRepository<ShiftReport>, IShiftReportRepository
    {
        public ShiftReportRepository(AppDbContext context) : base(context)
        {
        }
    }
}
