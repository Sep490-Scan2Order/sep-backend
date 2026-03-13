using Microsoft.AspNetCore.Http;
using ScanToOrder.Application.DTOs.Dishes;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface IDishService
    {
        Task<DishDto> CreateDish(Guid tenantId,int categoryId ,CreateDishRequest dishDto);
        Task<DishDto> CreateCombo(Guid tenantId, int categoryId, CreateComboRequest request);

        Task<DishDto> UpdateDish(Guid tenantId,int categoryId ,int dishId, UpdateDishRequest dishDto);

        Task<List<DishDto>> GetAllDishesByTenant(Guid tenantId, bool includeDeleted = false);

        Task<int> ImportDishesFromExcelAsync(Guid tenantId, IFormFile file);

        Task<bool> DeleteDish(Guid tenantId, int categoryId, int dishId);

        Task<bool> DeActiveDish(Guid tenantId, int categoryId, int dishId);

        Task<bool> ActiveDish(Guid tenantId, int categoryId, int dishId);
    }
}
