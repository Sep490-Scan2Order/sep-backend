using Pgvector;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Restaurants;

namespace ScanToOrder.Domain.Interfaces;

public interface ISearchRepository
{
    Task<List<(Restaurant Restaurant, double Distance)>> SearchRestaurantsByVectorAsync(Vector embedding, int topK = 10);
    Task<List<(Restaurant Restaurant, double Distance)>> SearchRestaurantsByKeywordAsync(string keyword, int topK = 10);
    
    Task<List<(Dish Dish, double Distance)>> SearchDishesByVectorAsync(Vector embedding, int topK = 10);
    Task<List<(Dish Dish, double Distance)>> SearchDishesByKeywordAsync(string keyword, int topK = 10);
}
