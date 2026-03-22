using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Api.Controllers
{
    public class RestaurantController : BaseController
    {
        private readonly IRestaurantService _restaurantService;
        private readonly IRestaurantMenuService _restaurantMenuService;
        private readonly IAuthenticatedUserService _authenticatedUserService;

        public RestaurantController(
            IRestaurantService restaurantService,
            IRestaurantMenuService restaurantMenuService,
            IAuthenticatedUserService authenticatedUserService)
        {
            _restaurantService = restaurantService;
            _restaurantMenuService = restaurantMenuService;
            _authenticatedUserService = authenticatedUserService;
        }

        // Restaurant Retrieval  
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<RestaurantDto>>> GetById(int id)
        {
            var result = await _restaurantService.GetRestaurantByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<RestaurantDto>.Failure(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND));
            return Success(result);
        }

        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<PagedRestaurantResultDto>>> GetAllPaged(
            [FromQuery] double? latitude,
            [FromQuery] double? longitude,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _restaurantService.GetRestaurantsPagedAsync(latitude, longitude, page, pageSize);
            return Success(result);
        }

        [HttpGet("nearby")]
        public async Task<ActionResult<ApiResponse<List<RestaurantDto>>>> GetNearby(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] double radiusKm = 10.0,
            [FromQuery] int limit = 10)
        {
            var result = await _restaurantService.GetNearbyRestaurantsAsync(latitude, longitude, radiusKm, limit);
            return Success(result);
        }

        [HttpGet("{slug}")]
        public async Task<ActionResult<ApiResponse<RestaurantDto>>> GetBySlug(string slug)
        {
            var result = await _restaurantService.GetRestaurantBySlugAsync(slug);
            if (result == null)
                throw new DomainException(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            return Success(result);
        }

        [HttpGet("{restaurantId:int}/menu")]
        public async Task<ActionResult<ApiResponse<List<MenuCategoryDto>>>> GetRestaurantMenu([FromRoute] int restaurantId)
        {
            var menu = await _restaurantMenuService.GetMenuForRestaurantAsync(restaurantId);
            return Success(menu);
        }
        
        [HttpGet("{restaurantId:int}/menu-all")]
        public async Task<ActionResult<ApiResponse<List<MenuCategoryDto>>>> GetRestaurantAllMenu([FromRoute] int restaurantId)
        {
            var menu = await _restaurantMenuService.GetAllMenuForRestaurantAsync(restaurantId);
            return Success(menu);
        }

        // Restaurant Management for Tenant
        [HttpPost]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<RestaurantDto>>> Create([FromForm] CreateRestaurantRequest request)
        {
            if (!_authenticatedUserService.ProfileId.HasValue)
            {
                return BadRequest(new { message = RestaurantMessage.RestaurantError.NOT_FOUND_RESTAURANT_FOR_USER });
            }

            var result = await _restaurantService.CreateRestaurantAsync(
                _authenticatedUserService.ProfileId.Value,
                request
            );

            return Success(result, RestaurantMessage.RestaurantSuccess.RESTAURANT_CREATED);
        }

        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<RestaurantDto>>> Update(int id, [FromForm] UpdateRestaurantRequest request)
        {
            if (!_authenticatedUserService.ProfileId.HasValue)
            {
                return BadRequest(new { message = RestaurantMessage.RestaurantError.NOT_FOUND_RESTAURANT_FOR_USER });
            }

            var result = await _restaurantService.UpdateRestaurantAsync(
                id,
                _authenticatedUserService.ProfileId.Value,
                request
            );

            return Success(result, RestaurantMessage.RestaurantSuccess.RESTAURANT_UPDATED);
        }

        [HttpGet("{slug}/qr-image")]
        [Produces("image/png")]
        public async Task<IActionResult> GetQrImageBySlug(string slug)
        {
            try
            {
                var imageBytes = await _restaurantService.GetRestaurantQrImageBySlugAsync(slug);

                return File(imageBytes, "image/png");
            }
            catch (DomainException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("get-all-restaurant-by-tenant")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<List<RestaurantDto>>>> GetByTenantId()
        {
            if (!_authenticatedUserService.ProfileId.HasValue)
            {
                throw new DomainException(RestaurantMessage.RestaurantError.NOT_FOUND_RESTAURANT_FOR_USER);
            }

            var result = await _restaurantService.GetRestaurantsByTenantIdAsync(_authenticatedUserService.ProfileId.Value);
            return Success(result.ToList());
        }

        [HttpPut("{id}/receiving-orders")]
        [Authorize(Roles = "Tenant, Staff, Cashier")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateReceivingOrders(int id, bool isReceivingOrders)
        {
            var result = await _restaurantService.UpdateReceivingOrdersAsync(id, isReceivingOrders);

            return Success(result);
        }

        [HttpPut("{id}/assign-present-cashier")]
        [Authorize(Roles = "Tenant, Staff, Cashier")]
        public async Task<ActionResult<ApiResponse<AssignPresentCashierDto>>> AssignPresentCashier(int id, [FromQuery] Guid cashierId)
        {
            var result = await _restaurantService.AssignPresentCashier(id, cashierId);

            return Success(result);
        }

        [HttpPut("config-min-cash-amount")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<string>>> ConfigMinCashAmount([FromQuery] int restaurantId, [FromQuery] decimal minCashAmount)
        {
            var result = await _restaurantService.ConfigMinCashAmountAsync(restaurantId, minCashAmount);

            return Success(result);
        }
    }
}
