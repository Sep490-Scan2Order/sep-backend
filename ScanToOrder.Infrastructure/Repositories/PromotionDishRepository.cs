using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class PromotionDishRepository : GenericRepository<PromotionDish>, IPromotionDishRepository
    {
        public PromotionDishRepository(AppDbContext context) : base(context)
        {
        }
    }
}

