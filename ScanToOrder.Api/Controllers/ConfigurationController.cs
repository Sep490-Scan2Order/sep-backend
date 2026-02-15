using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;
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
        public async Task<IActionResult> GetConfigurations()
        {
            var configurations = await _configurationService.GetConfigurationsAsync();
            return Ok(configurations);
        }
        [HttpPut]
        public async Task<IActionResult> UpdateConfigurations([FromBody] Configurations configurations)
        {
            var updatedConfig = await _configurationService.UpdateConfigurationsAsync(configurations);
            return Ok(updatedConfig);
        }
    }
}
