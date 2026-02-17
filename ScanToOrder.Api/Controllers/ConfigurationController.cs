using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Configuration;

namespace ScanToOrder.Api.Controllers
{
    public class ConfigurationController : BaseController
    {
        private readonly IConfigurationService _configurationService;
        public ConfigurationController(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }
        [HttpGet]
        public async Task<ApiResponse<Configurations>> GetConfigurations()
        {
            var configurations = await _configurationService.GetConfigurationsAsync();
            return new ApiResponse<Configurations>
            {
                IsSuccess = configurations.IsSuccess,
                Data = configurations.Data
            };
        }
        [HttpPut]
        public async Task<ApiResponse<Configurations>> UpdateConfigurations([FromBody] Configurations configurations)
        {
            var updatedConfig = await _configurationService.UpdateConfigurationsAsync(configurations);
            return new ApiResponse<Configurations>
            {
                IsSuccess = updatedConfig.IsSuccess,
                Data = updatedConfig.Data
            };
        }
    }
}
