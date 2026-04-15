using Hangfire;
using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace ScanToOrder.Infrastructure.Services
{
    public class OrderDataClearanceJob
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderDataClearanceJob(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task ExecuteAsync(int restaurantId)
        {
            // 1. Transactions
            var shiftIds = await _unitOfWork.Shifts.GetQueryable()
                .Where(s => s.RestaurantId == restaurantId && s.Note == "Seeded shift")
                .Select(s => s.Id)
                .ToListAsync();

            if (shiftIds.Any())
            {
                await _unitOfWork.Transactions.GetQueryable()
                    .Where(t => shiftIds.Contains(t.ShiftId ?? 0))
                    .ExecuteDeleteAsync();

                // 2. Shifts
                await _unitOfWork.Shifts.GetQueryable()
                    .Where(s => shiftIds.Contains(s.Id))
                    .ExecuteDeleteAsync();
            }

            // 3. Order Details
            var orderIds = await _unitOfWork.Orders.GetQueryable()
                .Where(o => o.RestaurantId == restaurantId && o.QrCodeUrl.Contains("api.qrserver.com"))
                .Select(o => o.Id)
                .ToListAsync();

            if (orderIds.Any())
            {
                await _unitOfWork.OrderDetails.GetQueryable()
                    .Where(od => orderIds.Contains(od.OrderId))
                    .ExecuteDeleteAsync();

                // 4. Orders
                await _unitOfWork.Orders.GetQueryable()
                    .Where(o => orderIds.Contains(o.Id))
                    .ExecuteDeleteAsync();
            }
        }
    }
}
