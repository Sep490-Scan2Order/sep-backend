using ScanToOrder.Domain.Entities.Vouchers;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class VoucherRepository : GenericRepository<Voucher>, IVoucherRepository
    {
        private readonly AppDbContext _context;
        public VoucherRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
