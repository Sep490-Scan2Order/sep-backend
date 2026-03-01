using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class CategoryController : BaseController
    {
        private readonly ICategoryService _categoryService;
        private readonly IAuthenticatedUserService _authenticatedUserService;

        public CategoryController(ICategoryService categoryService, IAuthenticatedUserService authenticatedUserService)
        {
            _categoryService = categoryService;
            _authenticatedUserService = authenticatedUserService;
        }

        [HttpGet("get-category-by-tenant")]
        public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetAllCategoriesByTenant()
        {
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var categories = await _categoryService.GetAllCategoriesByTenant(tenantId);
            return Success(categories, CategoryMessage.CategorySuccess.CATEGORY_RETRIEVED);
        }

        [HttpPost("create-category")]
        public async Task<ActionResult<ApiResponse<CategoryDto>>> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var category = await _categoryService.CreateCategory(tenantId, request);
            return Success(category, CategoryMessage.CategorySuccess.CATEGORY_CREATED);
        }

        [HttpPut("update-category/{id:int}")]
        public async Task<ActionResult<ApiResponse<CategoryDto>>> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
        {
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var category = await _categoryService.UpdateCategory(tenantId, id, request);
            return Success(category, CategoryMessage.CategorySuccess.CATEGORY_UPDATED);
        }
    }
}
