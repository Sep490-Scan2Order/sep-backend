using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class MenuTemplateController : BaseController
    {
        private readonly IMenuTemplateService _menuTemplateService;
        private readonly ILogger<MenuTemplateController> _logger;

        public MenuTemplateController(IMenuTemplateService menuTemplateService, ILogger<MenuTemplateController> logger)
        {
            _menuTemplateService = menuTemplateService;
            _logger = logger;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateTemplateResponseDto>>> CreateTemplate([FromForm] CreateTemplateRequestDto request)
        {   
            var result = await _menuTemplateService.CreateTemplateAsync(request);
            return Success(result);
        }

        [Authorize(Roles = "Admin, Tenant")]
        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<MenuTemplateDto>>>> GetTemplates()
        {
            var result = await _menuTemplateService.GetTemplatesAsync();
            return Success(result);
        }

        [Authorize(Roles = "Admin, Tenant")]
        [HttpGet("{templateId:int}")]
        public async Task<ActionResult<ApiResponse<MenuTemplateDto>>> GetTemplateById(int templateId)
        {
            var result = await _menuTemplateService.GetTemplateByIdAsync(templateId);
            return Success(result);
        }

        [Authorize(Roles = "Admin")]
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
        
        [Authorize(Roles = "Admin")]
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
                _logger.LogError(ex, "Lỗi khi tạo giao diện AI theo chủ đề ngày lễ. HolidayName: {HolidayName}", request?.HolidayName);
                return BadRequest(new ApiResponse<string>
                {
                    IsSuccess = false,
                    Message = "Không thể tạo giao diện AI lúc này. Vui lòng thử lại sau ít phút."
                });
            }
        }

        [Authorize(Roles = "Admin, Tenant")]
        [HttpGet("restaurant/{restaurantId:int}")]
        public async Task<ActionResult<ApiResponse<IEnumerable<MenuTemplateDto>>>> GetTemplatesForRestaurant(int restaurantId)
        {
            var result = await _menuTemplateService.GetTemplatesForRestaurantAsync(restaurantId);
            return Success(result);
        }
    }
}
