using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Configuration;
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
        public async Task<ActionResult<ApiResponse<ConfigurationResponse>>> GetConfigurations()
        {
            var configurations = await _configurationService.GetConfigurationsAsync();
            return Success(configurations);
        }
        [HttpPut]
        public async Task<ActionResult<ApiResponse<ConfigurationResponse>>> UpdateConfigurations([FromBody] Configurations configurations)
        {
            var updatedConfig = await _configurationService.UpdateConfigurationsAsync(configurations);
            return Success(updatedConfig);
        }
    }
}
