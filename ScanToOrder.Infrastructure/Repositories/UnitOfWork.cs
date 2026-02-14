using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;


namespace ScanToOrder.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IAuthenticationUserRepository AuthenticationUsers { get; }
        public IGenericRepository<Tenant> Tenants { get; }
        public IGenericRepository<Staff> Staffs { get; }
        public IGenericRepository<Restaurant> Restaurants { get; }
        public IGenericRepository<Customer> Customers { get; }
        public IGenericRepository<PointHistory> PointHistories { get; }
        public IGenericRepository<MemberPoint> MemberPoints { get; }

        public IGenericRepository<Plan> Plans { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            AuthenticationUsers = new AuthenticationUserRepository(_context);
            Tenants = new GenericRepository<Tenant>(_context);
            Staffs = new GenericRepository<Staff>(_context);
            Restaurants = new GenericRepository<Restaurant>(_context);
            Customers = new GenericRepository<Customer>(_context);
            PointHistories = new GenericRepository<PointHistory>(_context);
            MemberPoints = new GenericRepository<MemberPoint>(_context);
            Plans = new GenericRepository<Plan>(_context);
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
