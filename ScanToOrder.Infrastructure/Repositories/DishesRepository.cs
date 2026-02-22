using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class DishesRepository : GenericRepository<Dish>, IDishesRepository
    {
        private readonly AppDbContext _context;
        public DishesRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
