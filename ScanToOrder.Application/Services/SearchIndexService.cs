using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Application.Interfaces;
using Pgvector;

namespace ScanToOrder.Application.Services;

public class SearchIndexService : ISearchIndexService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOpenAiService _openAiService;

    public SearchIndexService(IUnitOfWork unitOfWork, IOpenAiService openAiService)
    {
        _unitOfWork = unitOfWork;
        _openAiService = openAiService;
    }

    public async Task IndexDishAsync(int dishId)
    {
        var dish = await _unitOfWork.Dishes.GetByFieldsIncludeAsync(d => d.Id == dishId, d => d.Category);
        if (dish == null) return;

        string searchText = $"{dish.DishName} {dish.Description} {dish.Category?.CategoryName}";
        if (string.IsNullOrWhiteSpace(searchText)) return;

        var floats = await _openAiService.GetEmbeddingAsync(searchText);
        dish.SearchVector = new Vector(floats);

        _unitOfWork.Dishes.Update(dish);
        await _unitOfWork.SaveAsync();
    }

    public async Task IndexRestaurantAsync(int restaurantId)
    {
        var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);
        if (restaurant == null) return;

        var dishes = await _unitOfWork.Dishes.GetAllAsync(d => d.Category.TenantId == restaurant.TenantId && d.IsAvailable && !d.IsDeleted);
        var topDishes = string.Join(", ", dishes.Take(10).Select(d => d.DishName));
        
        string searchText = $"{restaurant.RestaurantName} {restaurant.Description} {topDishes}";
        var floats = await _openAiService.GetEmbeddingAsync(searchText);
        restaurant.SearchVector = new Vector(floats);

        _unitOfWork.Restaurants.Update(restaurant);
        await _unitOfWork.SaveAsync();
    }

    public async Task FullReIndexAsync()
    {
        var allDishes = await _unitOfWork.Dishes.GetAllAsync(d => !d.IsDeleted);
        if (allDishes != null)
        {
            foreach (var d in allDishes) await IndexDishAsync(d.Id);
        }

        var allRes = await _unitOfWork.Restaurants.GetAllAsync(r => r.IsActive == true);
        if (allRes != null)
        {
            foreach (var r in allRes) await IndexRestaurantAsync(r.Id);
        }
    }
}
