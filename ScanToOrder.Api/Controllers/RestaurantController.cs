using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class RestaurantController : BaseController
    {
        private readonly IRestaurantService _restaurantService;

        public RestaurantController(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
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
        public async Task<ActionResult<ApiResponse<PagedRestaurantResultDto>>> GetAllSortedByDistancePaged(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _restaurantService.GetRestaurantsSortedByDistancePagedAsync(latitude, longitude, page, pageSize);
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
    }
}
