using ScanToOrder.Domain.Entities.Points;
using System;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IMemberPointRepository : IGenericRepository<MemberPoint>
    {
        Task<MemberPoint?> GetByAccountIdAsync(Guid accountId);
    }
}
