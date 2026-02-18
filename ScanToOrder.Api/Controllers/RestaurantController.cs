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
    }
}
