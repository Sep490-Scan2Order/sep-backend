using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Restaurants;
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

        public async Task<List<(int RestaurantId, string RestaurantName, string? Image,
      int TotalOrders, decimal TotalRevenue,
      string? PlanName, SubscriptionStatus? Status)>> GetTopRestaurantsFullDataAsync(int top)
        {
            var query = await _dbSet
                .GroupBy(o => o.RestaurantId)
                .Select(g => new
                {
                    RestaurantId = g.Key,
                    TotalOrders = g.Count(),
                    TotalRevenue = g.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(top)
                .Join(_context.Restaurants,
                    stat => stat.RestaurantId,
                    r => r.Id,
                    (stat, r) => new
                    {
                        stat.RestaurantId,
                        r.RestaurantName,
                        r.Image,
                        stat.TotalOrders,
                        stat.TotalRevenue,
                        PlanName = r.Subscription != null ? r.Subscription.Plan.Name : null,
                        Status = r.Subscription != null ? r.Subscription.Status : (SubscriptionStatus?)null
                    })
                .ToListAsync();

            return query.Select(x =>
                (x.RestaurantId, x.RestaurantName, x.Image,
                 x.TotalOrders, x.TotalRevenue,
                 x.PlanName, x.Status)
            ).ToList();
        }

        public async Task<List<Order>> GetCustomerActiveOrdersAsync(int restaurantId, string phone, int limit, int withinHours)
        {
            var cutoffCreatedUtc = DateTime.UtcNow.AddHours(-withinHours);
            var hideServedCancelledAfterUtc = DateTime.UtcNow.AddHours(-1);

            return await _dbSet
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .Where(o => o.RestaurantId == restaurantId
                            && !o.IsDeleted
                            && o.NumberPhone == phone
                            && o.CreatedAt >= cutoffCreatedUtc
                            && (
                                (o.Status != OrderStatus.Served && o.Status != OrderStatus.Cancelled)
                                || ((o.UpdatedAt ?? o.CreatedAt) >= hideServedCancelledAfterUtc)
                            ))
                .OrderByDescending(o => o.CreatedAt)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Order>> GetCustomerOrderHistoryAsync(int restaurantId, string phone, int limit)
        {
            var historyStatuses = new[]
            {
                OrderStatus.Served,
                OrderStatus.Cancelled
            };

            return await _dbSet
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .Where(o => o.RestaurantId == restaurantId
                            && !o.IsDeleted
                            && o.NumberPhone == phone
                            && historyStatuses.Contains(o.Status))
                .OrderByDescending(o => o.CreatedAt)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}

