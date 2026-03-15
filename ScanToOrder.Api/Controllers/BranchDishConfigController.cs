using Microsoft.AspNetCore.Authorization;
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
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<BranchDishConfigDto>>> ConfigDishByRestaurant([FromBody] CreateBranchDishConfig request)
        {
            var result = await _branchDishConfigService.ConfigDishByRestaurant(request);
            return Success(result);
        }

        [HttpGet("restaurants/{restaurantId}/branch-dishes")]
        [Authorize(Roles = "Tenant, Staff, Cashier")]
        public async Task<ActionResult<ApiResponse<List<BranchDishConfigDto>>>> GetBranchDishByRestaurant(int restaurantId)
        {
            var result = await _branchDishConfigService.GetBranchDishByRestaurant(restaurantId);

            return Success(result);
        }

        [HttpPut("toggle-sold-out/{restaurantId}/{dishId}")]
        [Authorize(Roles = "Staff, Cashier")]
        public async Task<ActionResult<ApiResponse<string>>> ToggleSoldOutStatus(int restaurantId, int dishId, [FromQuery] bool isSoldOut, int quantity)
        {
            var result = await _branchDishConfigService.UpdateIsSoldOutBranchDish(restaurantId, dishId, isSoldOut, quantity);
            return Success(result, "Cập nhật trạng thái hết món thành công.");
        }

    }
}
