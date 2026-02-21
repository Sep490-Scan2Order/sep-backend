using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class RestaurantPromotionRepository : GenericRepository<RestaurantPromotion>, IRestaurantPromotionRepository
    {
        private readonly AppDbContext _context;
        public RestaurantPromotionRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
