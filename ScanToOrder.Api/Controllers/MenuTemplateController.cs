using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class MenuTemplateController : BaseController
    {
        private readonly IMenuTemplateService _menuTemplateService;
        public MenuTemplateController(IMenuTemplateService menuTemplateService)
        {
            _menuTemplateService = menuTemplateService;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateTemplateResponseDto>>> CreateTemplate([FromBody] CreateTemplateRequestDto request)
        {
            var result = await _menuTemplateService.CreateTemplateAsync(request);
            return Success(result);
        }
    }
}
