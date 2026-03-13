using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class ComboDetailRepository : GenericRepository<ComboDetail>, IComboDetailRepository
    {
        public ComboDetailRepository(AppDbContext context) : base(context)
        {
        }
    }
}
