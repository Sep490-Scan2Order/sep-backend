using ScanToOrder.Domain.Entities.Vouchers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IMemberVoucherRepository : IGenericRepository<MemberVoucher>
    {
        Task<List<MemberVoucher>> GetActiveByUserIdAsync(Guid userId, DateTime now);
        Task<List<MemberVoucher>> GetExpiredByUserIdAsync(Guid userId, DateTime now);
    }
}
