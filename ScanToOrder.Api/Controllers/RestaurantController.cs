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

        [HttpGet] 
        public async Task<ActionResult<ApiResponse<List<RestaurantDto>>>> GetAll()
        {
            var result = await _restaurantService.GetAllRestaurantsAsync();
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
