using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Services.Def;

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
        public async Task<IActionResult> GetAll()
        {
            var result = await _restaurantService.GetAllRestaurantsAsync();
            return Success(result);
        }
    }
}
