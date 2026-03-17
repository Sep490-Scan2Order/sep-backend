using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;
using System.Linq;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class OrderRepository : GenericRepository<Order>, IOrderRepository
    {
        public OrderRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<int> GetNextDailyOrderCodeAsync(int restaurantId, DateTime startUtc, DateTime endUtc, int dateInt)
        {
            long lockKey = ((long)dateInt * 1_000_000L) + restaurantId;
            await _context.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({lockKey});");

            var maxToday = await _dbSet
                .Where(o => o.RestaurantId == restaurantId && o.CreatedAt >= startUtc && o.CreatedAt < endUtc)
                .Select(o => (int?)o.OrderCode)
                .MaxAsync() ?? 0;

            return maxToday + 1;
        }
        public async Task<List<Order>> GetOrdersForKdsAsync(int restaurantId)
        {
            return await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .Where(o => o.RestaurantId == restaurantId
                            && !o.IsDeleted)
                .OrderByDescending(o => o.CreatedAt) 
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Order?> GetOrderWithDetailsForKdsAsync(Guid orderId)
        {
            return await _dbSet
                .Include(o => o.OrderDetails)        
                    .ThenInclude(od => od.Dish)      
                .Include(o => o.Restaurant)   
                .Where(o => o.Id == orderId && !o.IsDeleted)
                .AsNoTracking()                      
                .FirstOrDefaultAsync();
        }

        public async Task<List<Order>> GetCashOrdersPendingConfirmAsync(int restaurantId)
        {
            return await _context.Orders
                .Where(o => o.RestaurantId == restaurantId
                            && !o.IsDeleted
                            && o.Status == OrderStatus.Unpaid
                            && _context.Transactions.Any(t =>
                                t.OrderId == o.Id &&
                                t.PaymentMethod == PaymentMethod.Cash &&
                                t.Status == OrderTransactionStatus.Pending))
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Order?> GetByOrderCodeAndRestaurantAsync(int orderCode, int restaurantId)
        {
            return await _dbSet
                .Where(o => o.RestaurantId == restaurantId && o.OrderCode == orderCode && !o.IsDeleted)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Order>> GetExpiredUnpaidOrdersAsync(int minuteThreshold)
        {
            var thresholdTime = DateTime.UtcNow.AddMinutes(-minuteThreshold);
            return await _dbSet
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .Where(o => o.Status == OrderStatus.Unpaid && !o.IsDeleted && o.CreatedAt <= thresholdTime)
                .ToListAsync();
        }

        public async Task<List<Order>> GetRecentByRestaurantAndPhoneAsync(int restaurantId, string phone, int limit)
        {
            var cutoffUtc = DateTime.UtcNow.AddHours(-1);
            return await _dbSet
                .Where(o => o.RestaurantId == restaurantId
                            && !o.IsDeleted
                            && o.NumberPhone == phone
                            && (o.Status != OrderStatus.Served && o.Status != OrderStatus.Cancelled
                                || ((o.UpdatedAt ?? o.CreatedAt) >= cutoffUtc)))
                .OrderByDescending(o => o.CreatedAt)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}

