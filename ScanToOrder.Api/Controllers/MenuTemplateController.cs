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
        public async Task<ActionResult<ApiResponse<CreateTemplateResponseDto>>> CreateTemplate([FromForm] CreateTemplateRequestDto request)
        {   
            var result = await _menuTemplateService.CreateTemplateAsync(request);
            return Success(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<MenuTemplateDto>>>> GetTemplates()
        {
            var result = await _menuTemplateService.GetTemplatesAsync();
            return Success(result);
        }

        [HttpGet("{templateId:int}")]
        public async Task<ActionResult<ApiResponse<MenuTemplateDto>>> GetTemplateById(int templateId)
        {
            var result = await _menuTemplateService.GetTemplateByIdAsync(templateId);
            return Success(result);
        }

        [HttpPut("{templateId:int}")]
        public async Task<ActionResult<ApiResponse<MenuTemplateDto>>> UpdateTemplate(
            int templateId,
            [FromBody] UpdateMenuTemplateDto request)
        {
            var result = await _menuTemplateService.UpdateTemplateAsync(templateId, request);
            return Success(result);
        }

        [HttpGet("restaurant/{restaurantId:int}/template")]
        public async Task<ActionResult<ApiResponse<MenuTemplateRenderDto>>> GetRestaurantMenuFromTemplate(
            int restaurantId)
        {
            var result = await _menuTemplateService.GetRestaurantMenuFromTemplateAsync(restaurantId);
            return Success(result);
        }

        [HttpPost("generate-holiday-ai")]
        public async Task<IActionResult> GenerateHolidayThemeAi([FromBody] AiHolidayTemplateRequestDto request)
        {
            try
            {
                var result = await _menuTemplateService.GenerateHolidayThemeAsync(request);
                return Ok(new ApiResponse<AiHolidayTemplateResponseDto>
                {
                    IsSuccess = true,
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<string>
                {
                    IsSuccess = false,
                    Message = ex.Message
                });
            }
        }
    }
}
