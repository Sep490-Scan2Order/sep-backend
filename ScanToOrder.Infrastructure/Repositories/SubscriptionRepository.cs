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
    }
}

