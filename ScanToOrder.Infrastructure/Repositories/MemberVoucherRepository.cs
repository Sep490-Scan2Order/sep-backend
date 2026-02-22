using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Vouchers;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class MemberVoucherRepository : GenericRepository<MemberVoucher>, IMemberVoucherRepository
    {
        private readonly AppDbContext _context;

        public MemberVoucherRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<MemberVoucher>> GetActiveByUserIdAsync(Guid userId, DateTime now)
        {
            return await _context.MemberVouchers
                .Include(mv => mv.Voucher)
                .Where(mv =>
                    mv.UserId == userId &&
                    !mv.IsDeleted &&
                    !mv.IsUsed &&
                    (mv.ExpiredAt == null || mv.ExpiredAt > now))
                .ToListAsync();
        }

        public async Task<List<MemberVoucher>> GetExpiredByUserIdAsync(Guid userId, DateTime now)
        {
            return await _context.MemberVouchers
                .Include(mv => mv.Voucher)
                .Where(mv =>
                    mv.UserId == userId &&
                    !mv.IsDeleted &&
                    (
                        mv.IsUsed ||
                        (mv.ExpiredAt != null && mv.ExpiredAt <= now)
                    ))
                .ToListAsync();
        }
    }
}
