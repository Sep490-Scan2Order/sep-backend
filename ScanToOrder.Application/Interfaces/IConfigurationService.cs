using ScanToOrder.Application.DTOs.Configuration;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Configuration;

namespace ScanToOrder.Application.Interfaces
{
    public interface IConfigurationService
    {
        Task<ConfigurationResponse> GetConfigurationsAsync();
        Task<ConfigurationResponse> UpdateConfigurationsAsync(Configurations configurations);
    }
}
