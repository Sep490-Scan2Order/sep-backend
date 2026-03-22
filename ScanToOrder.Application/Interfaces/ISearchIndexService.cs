namespace ScanToOrder.Application.Interfaces;

public interface ISearchIndexService
{
    Task IndexDishAsync(int dishId);
    Task IndexRestaurantAsync(int restaurantId);
    Task FullReIndexAsync();
}
