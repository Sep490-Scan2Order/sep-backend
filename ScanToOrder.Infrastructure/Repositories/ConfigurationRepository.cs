using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class ConfigurationRepository : GenericRepository<Configurations>, IConfigurationRepository
    {
        private readonly AppDbContext _context;
        public ConfigurationRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }
    }
}
