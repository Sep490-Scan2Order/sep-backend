using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class SubscriptionRepository : GenericRepository<Subscription>, ISubscriptionRepository
    {
        public SubscriptionRepository(AppDbContext context) : base(context)
        {
        }
        
        public async Task<Dictionary<int, Subscription>> GetByRestaurantIds (List<int> restaurantIds)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(r => restaurantIds.Contains(r.RestaurantId) && !r.IsDeleted && r.Status == SubscriptionStatus.Active)
                .Include(s => s.Plan)
                .ToDictionaryAsync(r => r.RestaurantId);
        }

        public async Task<List<(string PlanName, int Count)>> GetSubscriptionDistributionRawAsync()
        {
            var data = await _dbSet
                .Where(s => s.Status == SubscriptionStatus.Active)
                .GroupBy(s => s.Plan.Name)
                .Select(g => new
                {
                    PlanName = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            return data
                .Select(x => (x.PlanName, x.Count))
                .ToList();
        }

        public async Task<List<(int RestaurantId, string RestaurantName, string PlanName, DateTime ExpirationDate)>>
    GetExpiringSubscriptionsRawAsync(DateTime now, DateTime targetDate)
        {
            var data = await _dbSet
                .Where(s => s.Status == SubscriptionStatus.Active
                         && s.EndDate <= targetDate
                         && s.EndDate >= now)
                .Select(s => new
                {
                    s.RestaurantId,
                    RestaurantName = s.Restaurant.RestaurantName,
                    PlanName = s.Plan.Name,
                    ExpirationDate = s.EndDate
                })
                .OrderBy(s => s.ExpirationDate)
                .ToListAsync();

            return data
                .Select(x => (x.RestaurantId, x.RestaurantName, x.PlanName, x.ExpirationDate))
                .ToList();
        }
    }
}

