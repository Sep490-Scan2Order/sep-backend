using ScanToOrder.Application.DTOs.Dishes;

namespace ScanToOrder.Application.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryDto> CreateCategory(Guid tenantId, CreateCategoryRequest categoryDto);
        Task<CategoryDto> UpdateCategory(int categoryId, UpdateCategoryRequest categoryDto);

        Task<List<CategoryDto>> GetAllCategoriesByTenant(Guid tenantId);

        Task<bool> DeleteCategory(int categoryId);

        Task<bool> DeActiveCategory(int categoryId);

        Task<bool> ActiveCategory(int categoryId);
    }
}
