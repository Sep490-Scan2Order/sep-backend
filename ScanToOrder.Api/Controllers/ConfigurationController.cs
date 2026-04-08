using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Configuration;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers;

public class ConfigurationController : BaseController
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationController(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ConfigurationResponse?>>> GetConfigurations()
    {
        var configurations = await _configurationService.GetConfigurationsAsync();
        return Success(configurations);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ConfigurationResponse>>> UpdateConfigurations(
        int id,
        [FromBody] UpdateConfigurationRequest request)
    {
        var updated = await _configurationService.UpdateConfigurationsAsync(id, request);
        return Success(updated, "Cập nhật cấu hình thành công.");
    }
}
