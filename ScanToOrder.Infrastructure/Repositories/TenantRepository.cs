using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class TenantRepository : GenericRepository<Tenant>, ITenantRepository
    {
        private readonly AppDbContext _context;
        public TenantRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<Tenant>> GetTenantsWithSubscriptionsAsync()
        {
            return await _context.Tenants
                .Include(t => t.Subscriptions)
                    .ThenInclude(s => s.Plan)
                .ToListAsync();
        }
    }
}
