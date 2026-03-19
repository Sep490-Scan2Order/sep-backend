using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Api.Controllers
{
    public class BranchDishConfigController : BaseController
    {
        private readonly IBranchDishConfigService _branchDishConfigService;
        private readonly IAuthenticatedUserService _authenticatedUserService;

        public BranchDishConfigController(IBranchDishConfigService branchDishConfigService, IAuthenticatedUserService authenticatedUserService)
        {
            _branchDishConfigService = branchDishConfigService;
            _authenticatedUserService = authenticatedUserService;
        }

        [HttpPost("config-dish")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<BranchDishConfigDto>>> ConfigDishByRestaurant(
            [FromBody] CreateBranchDishConfig request)
        {
            var result = await _branchDishConfigService.ConfigDishByRestaurant(request);
            return Success(result);
        }

        [HttpGet("restaurants/{restaurantId}/branch-dishes")]
        [Authorize(Roles = "Tenant, Staff, Cashier")]
        public async Task<ActionResult<ApiResponse<List<BranchDishConfigDto>>>> GetBranchDishByRestaurant(
            int restaurantId)
        {
            var result = await _branchDishConfigService.GetBranchDishByRestaurant(restaurantId);

            return Success(result);
        }

        [HttpPut("toggle-sold-out/{restaurantId}/{dishId}")]
        [Authorize(Roles = "Staff, Cashier")]
        public async Task<ActionResult<ApiResponse<string>>> ToggleSoldOutStatus(int restaurantId, int dishId,
            [FromQuery] bool isSoldOut, int quantity)
        {
            var result =
                await _branchDishConfigService.UpdateIsSoldOutBranchDish(restaurantId, dishId, isSoldOut, quantity);
            return Success(string.Empty, result);
        }

        [HttpPut("update-is-selling/{restaurantId}/{dishId}")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateIsSellingStatus(int restaurantId, int dishId,
            [FromQuery] bool isSelling)
        {
            var result =
                await _branchDishConfigService.UpdateIsSellingBranchDish(restaurantId, dishId, isSelling);
            return Success(string.Empty, result);
        }

        [HttpPost("sync-dishes-to-branches")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<string>>> SyncDishesToBranchDishConfig()
        {
            if (_authenticatedUserService.ProfileId == null) 
                throw new DomainException(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
            
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var result = await _branchDishConfigService.SyncDishesToBranchDishConfigAsync(tenantId);
            
            return Success(string.Empty, result);
        }
    }
}