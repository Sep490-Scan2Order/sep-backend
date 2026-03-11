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
        [Consumes("multipart/form-data")]
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
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<DishDto>>> UpdateDish(int categoryId, int dishId, [FromForm] UpdateDishRequest request)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                var tenantId = _authenticatedUserService.ProfileId.Value;
                var dish = await dishService.UpdateDish(tenantId, categoryId, dishId, request);
                return Success(dish, DishMessage.DishSuccess.DISH_UPDATED);
            }
            throw new DomainException("ProfileId is null");
        }

        [HttpPost("import-dishes")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiResponse<int>>> ImportDishes(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Fail<int>(DishMessage.DishError.DISH_IMPORT_FILE_INVALID);
            }

            if (_authenticatedUserService.ProfileId == null)
            {
                throw new DomainException("ProfileId is null");
            }

            var tenantId = _authenticatedUserService.ProfileId.Value;
            var count = await dishService.ImportDishesFromExcelAsync(tenantId, file);

            return Success(count, $"Import thành công {count} món ăn");
        }

    }
}
