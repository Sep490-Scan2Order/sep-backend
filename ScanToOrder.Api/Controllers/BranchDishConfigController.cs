using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class BranchDishConfigController : BaseController
    {
        private readonly IBranchDishConfigService _branchDishConfigService;

        public BranchDishConfigController(IBranchDishConfigService branchDishConfigService)
        {
            _branchDishConfigService = branchDishConfigService;
        }

        [HttpPost("config-dish")]

        public async Task<ActionResult<ApiResponse<BranchDishConfigDto>>> ConfigDishByRestaurant([FromBody] CreateBranchDishConfig request)
        {
            var result = await _branchDishConfigService.ConfigDishByRestaurant(request);
            return Success(result);
        }

        [HttpGet("restaurants/{restaurantId}/branch-dishes")]
        public async Task<ActionResult<ApiResponse<List<BranchDishConfigDto>>>> GetBranchDishByRestaurant(int restaurantId)
        {
            var result = await _branchDishConfigService.GetBranchDishByRestaurant(restaurantId);

            return Success(result);
        }

        [HttpPut("toggle-sold-out/{id}")]
        public async Task<ActionResult<ApiResponse<BranchDishConfigDto>>> ToggleSoldOut(int id, [FromQuery] bool isSoldOut)
        {
            var result = await _branchDishConfigService.ToggleSoldOutAsync(id, isSoldOut);
            return Success(result);
        }
    }
}
