using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class MenuTemplateRepository : GenericRepository<MenuTemplate>, IMenuTemplateRepository
    {
        private readonly AppDbContext _context;
        public MenuTemplateRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
