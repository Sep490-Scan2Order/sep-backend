using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class BranchDishConfigRepository : GenericRepository<BranchDishConfig>, IBranchDishConfigRepository
    {
        private readonly AppDbContext _context;
        public BranchDishConfigRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<BranchDishConfig>> GetByRestaurantIdWithIncludeAsync(int restaurantId)
        {
            return await _context.BranchDishConfigs
                .Include(x => x.Restaurant)
                .Include(x => x.Dish)
                .Where(x => x.RestaurantId == restaurantId)
                .ToListAsync();
        }

        public async Task<BranchDishConfig?> GetByIdWithIncludeAsync(int id)
        {
            return await _context.BranchDishConfigs
                .Include(x => x.Restaurant)
                .Include(x => x.Dish)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task AddRangeAsync(List<BranchDishConfig> configs)
        {
            await _dbSet.AddRangeAsync(configs);
        }
        
        public async Task<List<BranchDishConfig>> GetSellingDishesAsync(int restaurantId)
        {
            return await _context.BranchDishConfigs
                .Include(bdc => bdc.Dish)
                    .ThenInclude(d => d.Category)
                .Include(bdc => bdc.Dish)
                    .ThenInclude(d => d.PromotionDishes) 
                        .ThenInclude(pd => pd.Promotion)
                .Where(bdc => bdc.RestaurantId == restaurantId 
                              && bdc.IsSelling 
                              && !bdc.IsDeleted 
                              && !bdc.Dish.IsDeleted)
                .ToListAsync();
        }
    }
}
