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

        [HttpGet("get-dishes-by-tenant-include-delete")]
        public async Task<ActionResult<ApiResponse<List<DishDto>>>> GetAllDishesByTenant()
        {
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var dishes = await dishService.GetAllDishesByTenant(tenantId, true);
            return Success(dishes, DishMessage.DishSuccess.DISH_RETRIEVED);
        }

        [HttpGet("get-dish-by-tenantId/{id:Guid}")]
        public async Task<ActionResult<ApiResponse<List<DishDto>>>> GetDishByTenantId(Guid id)
        {
            var dish = await dishService.GetAllDishesByTenant(id);
            return Success(dish, DishMessage.DishSuccess.DISH_RETRIEVED);
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

        [HttpDelete("delete-dish/{categoryId:int}/{dishId:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteDish(int categoryId, int dishId)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                var tenantId = _authenticatedUserService.ProfileId.Value;
                var result = await dishService.DeleteDish(tenantId, categoryId, dishId);
                return Success(result, DishMessage.DishSuccess.DISH_DELETED);
            }
            throw new DomainException("ProfileId is null");

        }

        [HttpPut("de-active-dish/{categoryId:int}/{dishId:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeActiveDish(int categoryId, int dishId)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                var tenantId = _authenticatedUserService.ProfileId.Value;
                var result = await dishService.DeActiveDish(tenantId, categoryId, dishId);
                return Success(result, DishMessage.DishSuccess.DISH_DEACTIVE);
            }
            throw new DomainException("ProfileId is null");
        }

        [HttpPut("active-dish/{categoryId:int}/{dishId:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> ActiveDish(int categoryId, int dishId)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                var tenantId = _authenticatedUserService.ProfileId.Value;
                var result = await dishService.ActiveDish(tenantId, categoryId, dishId);
                return Success(result, DishMessage.DishSuccess.DISH_ACTIVATED);
            }
            throw new DomainException("ProfileId is null");
        }
    }
}
