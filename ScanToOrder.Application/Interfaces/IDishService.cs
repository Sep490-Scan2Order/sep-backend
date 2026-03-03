using ScanToOrder.Application.DTOs.Dishes;

namespace ScanToOrder.Application.Interfaces
{
    public interface IDishService
    {
        Task<DishDto> CreateDish(Guid tenantId,int categoryId ,CreateDishRequest dishDto);

        Task<DishDto> UpdateDish(Guid tenantId,int categoryId ,int dishId, UpdateDishRequest dishDto);

        Task<List<DishDto>> GetAllDishesByTenant(Guid tenantId);

        Task<bool> UpdateDishAvailability(Guid tenantId, int dishId, int availabilityStatus);
    }
}
