using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;

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
            if (_authenticatedUserService.ProfileId == null) throw new DomainException(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var categories = await _categoryService.GetAllCategoriesByTenant(tenantId);
            return Success(categories, CategoryMessage.CategorySuccess.CATEGORY_RETRIEVED);
        }

        [HttpGet("get-category-by-tenantId/{id:Guid}")]
        public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetAllCategoriesByTenantId(Guid id)
        {
            var categories = await _categoryService.GetAllCategoriesByTenant(id);
            return Success(categories, CategoryMessage.CategorySuccess.CATEGORY_RETRIEVED);
        }

        [HttpPost("create-category")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<CategoryDto>>> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            if (_authenticatedUserService.ProfileId == null) throw new DomainException(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var category = await _categoryService.CreateCategory(tenantId, request);
            return Success(category, CategoryMessage.CategorySuccess.CATEGORY_CREATED);
        }

        [HttpPut("update-category/{id:int}")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<CategoryDto>>> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
        {
            if (_authenticatedUserService.ProfileId == null) throw new DomainException(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var category = await _categoryService.UpdateCategory(tenantId, id, request);
            return Success(category, CategoryMessage.CategorySuccess.CATEGORY_UPDATED);
        }
    }
}
