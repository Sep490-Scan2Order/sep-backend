using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Configuration;

namespace ScanToOrder.Application.Interfaces
{
    public interface IConfigurationService
    {
        Task<ApiResponse<Configurations>> GetConfigurationsAsync();
        Task<ApiResponse<Configurations>> UpdateConfigurationsAsync(Configurations configurations);
    }
}
