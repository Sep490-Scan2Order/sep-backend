using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class PromotionDishRepository : GenericRepository<PromotionDish>, IPromotionDishRepository
    {
        private readonly AppDbContext _context;
        public PromotionDishRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
