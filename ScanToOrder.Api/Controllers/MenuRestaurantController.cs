using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class MenuRestaurantController : BaseController
    {
        private readonly IMenuRestaurantService _menuRestaurantService;
        public MenuRestaurantController(IMenuRestaurantService menuRestaurantService)
        {
            _menuRestaurantService = menuRestaurantService;
        }
        [HttpGet("{restaurantId:int}")]
        public async Task<ActionResult<ApiResponse<MenuRestaurantDto>>> GetMenuByRestaurantId(int restaurantId)
        {
            var result = await _menuRestaurantService.GetMenuByRestaurantIdAsync(restaurantId);
            return Success(result);
        }
        [HttpPost]
        public async Task<ActionResult<ApiResponse<MenuRestaurantDto>>> ApplyRestaurantWithTemplate([FromBody] CreateMenuRestaurantRequestDto request)
        {
            var result = await _menuRestaurantService.ApplyRestaurantWithTemplateAsync(request);
            return Success(result);
        }
    }
}
