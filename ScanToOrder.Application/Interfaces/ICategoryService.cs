using ScanToOrder.Application.DTOs.Dishes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryDto> CreateCategory(Guid tenantId, CreateCategoryRequest categoryDto);
        Task<CategoryDto> UpdateCategory(Guid tenantId, int categoryId, UpdateCategoryRequest categoryDto);

        Task<List<CategoryDto>> GetAllCategoriesByTenant(Guid tenantId);
    }
}
