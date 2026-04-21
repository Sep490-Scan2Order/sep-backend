using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class ShiftTransferRepository : GenericRepository<ShiftTransfer>, IShiftTransferRepository
    {
        public ShiftTransferRepository(AppDbContext dbContext) : base(dbContext)
        {
        }
    }
}
