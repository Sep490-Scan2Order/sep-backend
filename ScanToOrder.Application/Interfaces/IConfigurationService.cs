using ScanToOrder.Domain.Entities.Configuration;

namespace ScanToOrder.Application.Interfaces
{
    public interface IConfigurationService
    {
        Task<Configurations> GetConfigurationsAsync();
        Task<Configurations> UpdateConfigurationsAsync(Configurations configurations);
    }
}
