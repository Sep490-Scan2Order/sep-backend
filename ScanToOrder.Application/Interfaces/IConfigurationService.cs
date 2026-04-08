using ScanToOrder.Application.DTOs.Configuration;

namespace ScanToOrder.Application.Interfaces;

public interface IConfigurationService
{
    Task<ConfigurationResponse?> GetConfigurationsAsync();
    Task<ConfigurationResponse> UpdateConfigurationsAsync(int id, UpdateConfigurationRequest request);
}
