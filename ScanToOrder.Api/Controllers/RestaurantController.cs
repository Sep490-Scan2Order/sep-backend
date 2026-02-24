using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using System.Security.Claims;

namespace ScanToOrder.Api.Controllers
{
    public class RestaurantController : BaseController
    {
        private readonly IRestaurantService _restaurantService;
        private readonly IAuthenticatedUserService _authenticatedUserService;

        public RestaurantController(IRestaurantService restaurantService, IAuthenticatedUserService authenticatedUserService)
        {
            _restaurantService = restaurantService;
            _authenticatedUserService = authenticatedUserService;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<RestaurantDto>>> GetById(int id)
        {
            var result = await _restaurantService.GetRestaurantByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<RestaurantDto>.Failure("Nhà hàng không tồn tại."));
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
            [FromQuery] double radiusKm = 5.0,
            [FromQuery] int limit = 10)
        {
            var result = await _restaurantService.GetNearbyRestaurantsAsync(latitude, longitude, radiusKm, limit);
            return Success(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<RestaurantDto>>> Create([FromBody] CreateRestaurantRequest request)
        {
           
            var result = await _restaurantService.CreateRestaurantAsync(_authenticatedUserService.ProfileId.Value, request);

            return Success(result, "Tạo nhà hàng mới thành công.");
        }
    }
}
