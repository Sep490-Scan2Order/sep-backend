using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Models;
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
                .Include(o => o.Promotion)
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

        public async Task<Order?> GetOrderWithDetailsByIdAsync(Guid orderId)
        {
            return await _dbSet
                .Include(o => o.OrderDetails)
                .Where(o => o.Id == orderId && !o.IsDeleted)
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
                .Include(o => o.Promotion)
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
            var baseQuery = _dbSet.Where(o => !o.IsDeleted && o.RestaurantId == restaurantId);

            return await baseQuery
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .Where(o =>
                    o.NumberPhone == phone
                    ||
                    (o.typeOrder == TypeOrder.Refund
                     && o.RefundOrderId != null
                     && baseQuery.Any(root =>
                         root.Id == o.RefundOrderId
                         && root.NumberPhone == phone)))
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Order>> GetCustomerActiveOrdersAllRestaurantsAsync(string phone)
        {
            var baseQuery = _dbSet.Where(o => !o.IsDeleted);

            return await baseQuery
                .Include(o => o.Restaurant)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .Where(o =>
                    o.NumberPhone == phone
                    ||                    
                    (o.typeOrder == TypeOrder.Refund
                     && o.RefundOrderId != null
                     && baseQuery.Any(root =>
                         root.Id == o.RefundOrderId
                         && root.NumberPhone == phone)))
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<OrderRevenueMetrics> GetRevenueSummaryAsync(int restaurantId, DateTime startDate, DateTime endDate)
        {
            var query = _dbSet.AsNoTracking()
                .Where(o => o.RestaurantId == restaurantId
                         && (o.Status == OrderStatus.Served || (o.typeOrder == TypeOrder.Refund && o.Status == OrderStatus.Cancelled))
                         && o.CreatedAt >= startDate
                         && o.CreatedAt <= endDate);

            var metrics = await query
                .Select(o => new
                {
                    o.typeOrder,
                    o.Type,
                    o.TotalAmount,
                    o.FinalAmount,
                    o.PromotionDiscount,
                    o.RefundOrderId,
                    o.RefundType
                })
                .GroupJoin(_context.Orders.AsNoTracking(), 
                    o => o.RefundOrderId, 
                    orig => (Guid?)orig.Id, 
                    (o, origs) => new { o, origs })
                .SelectMany(x => x.origs.DefaultIfEmpty(), 
                    (x, orig) => new { x.o, orig })
                .Select(x => new 
                {
                    x.o.typeOrder,
                    x.o.Type,
                    x.o.TotalAmount,
                    x.o.FinalAmount,
                    x.o.PromotionDiscount,
                    x.o.RefundType,
                    IsOriginalServed = x.o.typeOrder == TypeOrder.Refund && x.orig != null && x.orig.Status == OrderStatus.Served
                })
                .GroupBy(o => 1)
                .Select(g => new OrderRevenueMetrics
                {
                    TotalOrders = g.Count(o => o.typeOrder == TypeOrder.Regular),
                    GrossRevenue = g.Where(o => o.typeOrder == TypeOrder.Regular).Sum(o => o.TotalAmount),
                    NetRevenue = g.Where(o => o.typeOrder == TypeOrder.Regular).Sum(o => o.FinalAmount) 
                                 - g.Where(o => o.typeOrder == TypeOrder.Refund && o.IsOriginalServed).Sum(o => o.FinalAmount),
                    TotalDiscount = g.Where(o => o.typeOrder == TypeOrder.Regular).Sum(o => o.PromotionDiscount),
                    
                    RegularCount = g.Count(o => o.typeOrder == TypeOrder.Regular),
                    RegularRevenue = g.Where(o => o.typeOrder == TypeOrder.Regular).Sum(o => o.FinalAmount),
                    
                    RefundCount = g.Count(o => o.typeOrder == TypeOrder.Refund),
                    RefundRevenue = g.Where(o => o.typeOrder == TypeOrder.Refund).Sum(o => o.FinalAmount),
                    
                    RefundObjectiveCount = g.Count(o => o.typeOrder == TypeOrder.Refund && o.RefundType == RefundType.Objective),
                    RefundObjectiveRevenue = g.Where(o => o.typeOrder == TypeOrder.Refund && o.RefundType == RefundType.Objective).Sum(o => o.FinalAmount),
                    
                    RefundStaffErrorCount = g.Count(o => o.typeOrder == TypeOrder.Refund && o.RefundType == RefundType.StaffError),
                    RefundStaffErrorRevenue = g.Where(o => o.typeOrder == TypeOrder.Refund && o.RefundType == RefundType.StaffError).Sum(o => o.FinalAmount),
                    
                    RefundSystemErrorCount = g.Count(o => o.typeOrder == TypeOrder.Refund && o.RefundType == RefundType.SystemError),
                    RefundSystemErrorRevenue = g.Where(o => o.typeOrder == TypeOrder.Refund && o.RefundType == RefundType.SystemError).Sum(o => o.FinalAmount),

                    TotalCash = g.Sum(x => x.typeOrder == TypeOrder.Regular && x.Type == "Cash" ? x.FinalAmount : (x.IsOriginalServed && x.Type == "Cash" ? -x.FinalAmount : 0)),
                    TotalTransfer = g.Sum(x => x.typeOrder == TypeOrder.Regular && x.Type != "Cash" ? x.FinalAmount : (x.IsOriginalServed && x.Type != "Cash" ? -x.FinalAmount : 0))
                })
                .FirstOrDefaultAsync();

            return metrics ?? new OrderRevenueMetrics();
        }

        public async Task<List<(int DishId, string DishName, int QuantitySold, decimal Revenue)>> GetTopSellingDishesAsync(int restaurantId, DateTime startDate, DateTime endDate, int top)
        {
            return await _context.OrderDetails.AsNoTracking()
                .Where(od => od.Order.RestaurantId == restaurantId 
                          && (od.Order.Status == OrderStatus.Served || (od.Order.typeOrder == TypeOrder.Refund && od.Order.Status == OrderStatus.Cancelled))
                          && od.Order.CreatedAt >= startDate 
                          && od.Order.CreatedAt <= endDate)
                .Select(od => new
                {
                    od.DishId,
                    od.Dish.DishName,
                    od.Quantity,
                    od.SubTotal,
                    od.Order.typeOrder,
                    od.Order.RefundOrderId
                })
                .GroupJoin(_context.Orders.AsNoTracking(),
                    od => od.RefundOrderId,
                    orig => (Guid?)orig.Id,
                    (od, origs) => new { od, origs })
                .SelectMany(x => x.origs.DefaultIfEmpty(),
                    (x, orig) => new
                    {
                        x.od.DishId,
                        x.od.DishName,
                        x.od.Quantity,
                        x.od.SubTotal,
                        x.od.typeOrder,
                        IsOriginalServed = x.od.typeOrder == TypeOrder.Refund && orig != null && orig.Status == OrderStatus.Served
                    })
                .GroupBy(od => new { od.DishId, od.DishName })
                .Select(g => new
                {
                    g.Key.DishId,
                    g.Key.DishName,
                    QuantitySold = g.Sum(x => x.typeOrder == TypeOrder.Regular ? x.Quantity : (x.IsOriginalServed ? -x.Quantity : 0)),
                    Revenue = g.Sum(x => x.typeOrder == TypeOrder.Regular ? x.SubTotal : (x.IsOriginalServed ? -x.SubTotal : 0))
                })
                .Where(x => x.QuantitySold > 0)
                .OrderByDescending(x => x.QuantitySold)
                .Take(top)
                .Select(x => new ValueTuple<int, string, int, decimal>(x.DishId, x.DishName, (int)x.QuantitySold, x.Revenue))
                .ToListAsync();
        }

        public async Task<List<(Guid TenantId, string TenantName, int TotalRestaurants, int TotalOrders, decimal TotalRevenue)>>
            GetTopTenantsByRevenueAsync(int top)
        {
            var result = await _dbSet
                .AsNoTracking()
                .Where(o => o.Status == OrderStatus.Served || (o.typeOrder == TypeOrder.Refund && o.Status == OrderStatus.Cancelled))
                .Select(o => new
                {
                    o.Restaurant.TenantId,
                    TenantName = o.Restaurant.Tenant.Name,
                    o.RestaurantId,
                    o.typeOrder,
                    o.FinalAmount,
                    o.RefundOrderId
                })
                .GroupJoin(_context.Orders.AsNoTracking(),
                    o => o.RefundOrderId,
                    orig => (Guid?)orig.Id,
                    (o, origs) => new { o, origs })
                .SelectMany(x => x.origs.DefaultIfEmpty(),
                    (x, orig) => new
                    {
                        x.o.TenantId,
                        x.o.TenantName,
                        x.o.RestaurantId,
                        x.o.typeOrder,
                        x.o.FinalAmount,
                        IsOriginalServed = x.o.typeOrder == TypeOrder.Refund && orig != null && orig.Status == OrderStatus.Served
                    })
                .GroupBy(o => new { o.TenantId, o.TenantName })
                .Select(g => new
                {
                    TenantId       = g.Key.TenantId,
                    TenantName     = g.Key.TenantName ?? string.Empty,
                    TotalRestaurants = g.Select(o => o.RestaurantId).Distinct().Count(),
                    TotalOrders    = g.Count(o => o.typeOrder == TypeOrder.Regular),
                    TotalRevenue   = g.Sum(o => o.typeOrder == TypeOrder.Regular ? o.FinalAmount : (o.IsOriginalServed ? -o.FinalAmount : 0))
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(top)
                .ToListAsync();

            return result
                .Select(x => (x.TenantId, x.TenantName, x.TotalRestaurants, x.TotalOrders, x.TotalRevenue))
                .ToList();
        }

        public async Task<List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>>
            GetRevenueByTenantAsync(Guid tenantId, DateTime? startDate, DateTime? endDate)
        {
            var query = _dbSet.AsNoTracking()
                .Where(o => o.Restaurant.TenantId == tenantId
                         && (o.Status == OrderStatus.Served || (o.typeOrder == TypeOrder.Refund && o.Status == OrderStatus.Cancelled)));

            if (startDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= endDate.Value);
            }

            var result = await query
                .Select(o => new
                {
                    o.RestaurantId,
                    o.typeOrder,
                    o.TotalAmount,
                    o.FinalAmount,
                    o.PromotionDiscount,
                    o.RefundOrderId
                })
                .GroupJoin(_context.Orders.AsNoTracking(),
                    o => o.RefundOrderId,
                    orig => (Guid?)orig.Id,
                    (o, origs) => new { o, origs })
                .SelectMany(x => x.origs.DefaultIfEmpty(),
                    (x, orig) => new
                    {
                        x.o.RestaurantId,
                        x.o.typeOrder,
                        x.o.TotalAmount,
                        x.o.FinalAmount,
                        x.o.PromotionDiscount,
                        IsOriginalServed = x.o.typeOrder == TypeOrder.Refund && orig != null && orig.Status == OrderStatus.Served
                    })
                .GroupBy(o => o.RestaurantId)
                .Select(g => new
                {
                    RestaurantId  = g.Key,
                    TotalOrders   = g.Count(o => o.typeOrder == TypeOrder.Regular),
                    GrossRevenue  = g.Sum(o => o.typeOrder == TypeOrder.Regular ? o.TotalAmount : 0),
                    NetRevenue    = g.Sum(o => o.typeOrder == TypeOrder.Regular ? o.FinalAmount : (o.IsOriginalServed ? -o.FinalAmount : 0)),
                    TotalDiscount = g.Sum(o => o.typeOrder == TypeOrder.Regular ? o.PromotionDiscount : 0)
                })
                .ToListAsync();

            return result
                .Select(x => (x.RestaurantId, x.TotalOrders, x.GrossRevenue, x.NetRevenue, x.TotalDiscount))
                .ToList();
        }
        public async Task<(List<Order> Items, int TotalCount)> GetTenantOrdersPagedAsync(
            int restaurantId,
            int pageIndex,
            int pageSize,
            string? keyword = null,
            OrderStatus? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            TypeOrder? typeOrder = null,
            RefundType? refundType = null)
        {
            var query = _dbSet.AsNoTracking()
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Dish)
                .Where(o => o.RestaurantId == restaurantId && !o.IsDeleted);

            if (!string.IsNullOrEmpty(keyword))
            {
                var search = keyword.Trim().ToLower();
                query = query.Where(o => o.OrderCode.ToString() == search || o.NumberPhone.Contains(search));
            }

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            if (typeOrder.HasValue)
            {
                query = query.Where(o => o.typeOrder == typeOrder.Value);
            }

            if (refundType.HasValue)
            {
                query = query.Where(o => o.RefundType == refundType.Value);
            }

            if (fromDate.HasValue)
            {
                var start = fromDate.Value.ToUniversalTime();
                query = query.Where(o => o.CreatedAt >= start);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.ToUniversalTime();
                query = query.Where(o => o.CreatedAt <= end);
            }

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
