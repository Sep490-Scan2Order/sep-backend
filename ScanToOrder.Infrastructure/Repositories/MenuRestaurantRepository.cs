using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class MenuRestaurantRepository : GenericRepository<MenuRestaurant>, IMenuRestaurantRepository
    {
        public MenuRestaurantRepository(AppDbContext context) : base(context)
        {
        }
    }
}

