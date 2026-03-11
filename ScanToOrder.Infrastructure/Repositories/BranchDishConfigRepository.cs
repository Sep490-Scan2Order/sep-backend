using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class BranchDishConfigRepository : GenericRepository<BranchDishConfig>, IBranchDishConfigRepository
    {
        public BranchDishConfigRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<BranchDishConfig>> GetByRestaurantIdWithIncludeAsync(int restaurantId)
        {
            return await _dbSet
                .Include(x => x.Restaurant)
                .Include(x => x.Dish)
                .Where(x => x.RestaurantId == restaurantId)
                .ToListAsync();
        }

        public async Task<BranchDishConfig?> GetByIdWithIncludeAsync(int id)
        {
            return await _dbSet
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
            return await _dbSet
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

        public async Task<List<BranchDishConfig>> GetConfigsByDishIdsAsync(List<int> dishIds)
        {
            // Dùng Contains để tìm tất cả Config có DishId nằm trong danh sách truyền vào
            return await _dbSet
                            .Where(b => dishIds
                            .Contains(b.DishId))
                            .ToListAsync();
        }
    }
}
