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
    }
}
