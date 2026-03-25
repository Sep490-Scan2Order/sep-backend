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

        public async Task<List<Order>> GetCustomerActiveOrdersAsync(int restaurantId, string phone)
        {
            return await _dbSet
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .Where(o => o.RestaurantId == restaurantId
                            && !o.IsDeleted
                            && o.NumberPhone == phone)
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Order>> GetCustomerActiveOrdersAllRestaurantsAsync(string phone)
        {
            return await _dbSet
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .Where(o => !o.IsDeleted
                            && o.NumberPhone == phone)
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<(int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount, int RegularCount, decimal RegularRevenue, int RefundCount, decimal RefundRevenue)> GetRevenueMetricsAsync(int restaurantId, DateTime startDate, DateTime endDate)
        {
            var query = _dbSet.AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId
                         && o.Status == OrderStatus.Served
                         && o.CreatedAt >= startDate
                         && o.CreatedAt <= endDate);

            var metrics = await query
                .GroupBy(o => 1)
                .Select(g => new
                {
                    TotalOrders = g.Count(),
                    GrossRevenue = g.Sum(o => o.TotalAmount),
                    NetRevenue = g.Sum(o => o.FinalAmount),
                    TotalDiscount = g.Sum(o => o.PromotionDiscount),
                    RegularCount = g.Count(o => o.typeOrder == TypeOrder.Regular),
                    RegularRevenue = g.Where(o => o.typeOrder == TypeOrder.Regular).Sum(o => o.FinalAmount),
                    RefundCount = g.Count(o => o.typeOrder == TypeOrder.Refund),
                    RefundRevenue = g.Where(o => o.typeOrder == TypeOrder.Refund).Sum(o => o.FinalAmount)
                })
                .FirstOrDefaultAsync();

            if (metrics == null) return (0, 0, 0, 0, 0, 0, 0, 0);

            return (
                metrics.TotalOrders, 
                metrics.GrossRevenue, 
                metrics.NetRevenue, 
                metrics.TotalDiscount, 
                metrics.RegularCount, 
                metrics.RegularRevenue, 
                metrics.RefundCount, 
                metrics.RefundRevenue
            );
        }

        public async Task<List<(int DishId, string DishName, int QuantitySold, decimal Revenue)>> GetTopSellingDishesAsync(int restaurantId, DateTime startDate, DateTime endDate, int top)
        {
            return await _context.OrderDetails.AsNoTracking()
                .Where(od => od.Order.RestaurantId == restaurantId 
                          && od.Order.Status == OrderStatus.Served 
                          && od.Order.CreatedAt >= startDate 
                          && od.Order.CreatedAt <= endDate)
                .GroupBy(od => new { od.DishId, od.Dish.DishName })
                .Select(g => new
                {
                    g.Key.DishId,
                    g.Key.DishName,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.SubTotal)
                })
                .OrderByDescending(x => x.QuantitySold)
                .Take(top)
                .Select(x => new ValueTuple<int, string, int, decimal>(x.DishId, x.DishName, x.QuantitySold, x.Revenue))
                .ToListAsync();
        }

        public async Task<List<(Guid TenantId, string TenantName, int TotalRestaurants, int TotalOrders, decimal TotalRevenue)>>
            GetTopTenantsByRevenueAsync(int top)
        {
            var result = await _dbSet
                .AsNoTracking()
                .Where(o => o.Status == OrderStatus.Served)
                .GroupBy(o => new { o.Restaurant.TenantId, o.Restaurant.Tenant.Name })
                .Select(g => new
                {
                    TenantId       = g.Key.TenantId,
                    TenantName     = g.Key.Name ?? string.Empty,
                    TotalRestaurants = g.Select(o => o.RestaurantId).Distinct().Count(),
                    TotalOrders    = g.Count(),
                    TotalRevenue   = g.Sum(o => o.FinalAmount)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(top)
                .ToListAsync();

            return result
                .Select(x => (x.TenantId, x.TenantName, x.TotalRestaurants, x.TotalOrders, x.TotalRevenue))
                .ToList();
        }
    }
}

