using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Api.Controllers
{
    public class DishController : BaseController
    {
        private readonly IDishService dishService;
        private readonly IAuthenticatedUserService _authenticatedUserService;

        public DishController(IDishService dishService, IAuthenticatedUserService authenticatedUserService)
        {
            this.dishService = dishService;
            _authenticatedUserService = authenticatedUserService;
        }

        [HttpGet("get-dishes-by-tenant")]
        public async Task<ActionResult<ApiResponse<List<DishDto>>>> GetAllDishesByTenant()
        {
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var dishes = await dishService.GetAllDishesByTenant(tenantId);
            return Success(dishes, DishMessage.DishSuccess.DISH_RETRIEVED);
        }

        [HttpPost("create-dish/{categoryId:int}")]
        public async Task<ActionResult<ApiResponse<DishDto>>> CreateDish(int categoryId, [FromForm] CreateDishRequest request)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                var tenantId = _authenticatedUserService.ProfileId.Value;
                var dish = await dishService.CreateDish(tenantId, categoryId, request);
                return Success(dish, DishMessage.DishSuccess.DISH_CREATED);
            }

            throw new DomainException("ProfileId is null");
        }

        [HttpPut("update-dish/{categoryId:int}/{dishId:int}")]
        public async Task<ActionResult<ApiResponse<DishDto>>> UpdateDish(int categoryId, int dishId, [FromBody] UpdateDishRequest request)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                var tenantId = _authenticatedUserService.ProfileId.Value;
                var dish = await dishService.UpdateDish(tenantId, categoryId, dishId, request);
                return Success(dish, DishMessage.DishSuccess.DISH_UPDATED);
            }
            throw new DomainException("ProfileId is null");
        }

        [HttpPut("update-dish-availability/{dishId:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateDishAvailability(int dishId, [FromQuery] int availabilityStatus)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                var tenantId = _authenticatedUserService.ProfileId.Value;
                var result = await dishService.UpdateDishAvailability(tenantId, dishId, availabilityStatus);
                return Success(result, DishMessage.DishSuccess.DISH_AVAILABILITY_UPDATED);
            }
            throw new DomainException("ProfileId is null");
        }
    }
}
