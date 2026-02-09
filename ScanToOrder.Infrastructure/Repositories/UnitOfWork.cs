using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;


namespace ScanToOrder.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IGenericRepository<AuthenticationUser> AuthenticationUsers { get; }

        public IGenericRepository<Tenant> Tenants { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            AuthenticationUsers = new GenericRepository<AuthenticationUser>(_context);
            Tenants = new GenericRepository<Tenant>(_context);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
