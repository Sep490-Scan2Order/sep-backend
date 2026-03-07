using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class RestaurantPromotionRepository : GenericRepository<RestaurantPromotion>, IRestaurantPromotionRepository
    {
        public RestaurantPromotionRepository(AppDbContext context) : base(context)
        {
        }
    }
}

