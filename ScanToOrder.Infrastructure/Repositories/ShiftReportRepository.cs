using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class ShiftReportRepository : GenericRepository<ShiftReport>, IShiftReportRepository
    {
        public ShiftReportRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<(ShiftReport Report, decimal OpeningCashAmount)>> GetReportsByRestaurantAsync(
            int restaurantId, DateTime? from, DateTime? to)
        {
            var query = _context.ShiftReports
                .AsNoTracking()
                .Join(
                    _context.Shifts.Where(s => s.RestaurantId == restaurantId && s.Status == ShiftStatus.Closed),
                    report => report.ShiftId,
                    shift => shift.Id,
                    (report, shift) => new { Report = report, shift.OpeningCashAmount }
                )
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(x => x.Report.ReportDate >= from.Value.ToUniversalTime());

            if (to.HasValue)
                query = query.Where(x => x.Report.ReportDate <= to.Value.ToUniversalTime());

            var results = await query
                .OrderByDescending(x => x.Report.ReportDate)
                .ToListAsync();

            return results
                .Select(x => (x.Report, x.OpeningCashAmount))
                .ToList();
        }

        public async Task<List<(ShiftReport Report, decimal OpeningCashAmount)>> GetReportsByStaffAsync(Guid staffId)
        {
            var results = await _context.ShiftReports
                .AsNoTracking()
                .Join(
                    _context.Shifts.Where(s => s.StaffId == staffId && s.Status == ShiftStatus.Closed),
                    report => report.ShiftId,
                    shift => shift.Id,
                    (report, shift) => new { Report = report, shift.OpeningCashAmount }
                )
                .OrderByDescending(x => x.Report.ReportDate)
                .ToListAsync();

            return results
                .Select(x => (x.Report, x.OpeningCashAmount))
                .ToList();
        }
    }
}
