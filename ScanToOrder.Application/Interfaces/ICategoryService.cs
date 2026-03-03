using ScanToOrder.Application.DTOs.Dishes;

namespace ScanToOrder.Application.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryDto> CreateCategory(Guid tenantId, CreateCategoryRequest categoryDto);
        Task<CategoryDto> UpdateCategory(Guid tenantId, int categoryId, UpdateCategoryRequest categoryDto);

        Task<List<CategoryDto>> GetAllCategoriesByTenant(Guid tenantId);
    }
}
