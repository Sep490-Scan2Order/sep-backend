using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories;

public class SearchRepository : ISearchRepository
{
    private readonly AppDbContext _context;

    public SearchRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<(Restaurant Restaurant, double Distance)>> SearchRestaurantsByVectorAsync(Vector embedding, int topK = 10)
    {
        var query = _context.Restaurants
            .Where(r => r.IsActive == true && r.SearchVector != null)
            .OrderBy(r => r.SearchVector!.CosineDistance(embedding))
            .Take(topK)
            .Select(r => new { Restaurant = r, Distance = r.SearchVector!.CosineDistance(embedding) });

        var results = await query.ToListAsync();
        return results.Select(x => (x.Restaurant, x.Distance)).ToList();
    }

    public async Task<List<(Restaurant Restaurant, double Distance)>> SearchRestaurantsByKeywordAsync(string keyword, int topK = 10)
    {
        var pattern = $"%{keyword}%";
        var query = _context.Restaurants
            .Where(r => r.IsActive == true && 
                       (EF.Functions.ILike(r.RestaurantName, pattern) || EF.Functions.ILike(r.Description, pattern)))
            .Take(topK);

        var results = await query.ToListAsync();
        return results.Select(x => (x, 0.0)).ToList();
    }

    public async Task<List<(Dish Dish, double Distance)>> SearchDishesByVectorAsync(Vector embedding, int topK = 10)
    {
        var query = _context.Dishes
            .Include(d => d.Category)
            .Where(d => !d.IsDeleted && d.IsAvailable && d.SearchVector != null)
            .OrderBy(d => d.SearchVector!.CosineDistance(embedding))
            .Take(topK)
            .Select(d => new { Dish = d, Distance = d.SearchVector!.CosineDistance(embedding) });

        var results = await query.ToListAsync();
        return results.Select(x => (x.Dish, x.Distance)).ToList();
    }
    public async Task<List<(Dish Dish, double Distance)>> SearchDishesByKeywordAsync(string keyword, int topK = 10)
    {
        var pattern = $"%{keyword}%";
        var query = _context.Dishes
            .Include(d => d.Category)
            .Where(d => !d.IsDeleted && d.IsAvailable && 
                       (EF.Functions.ILike(d.DishName, pattern) || EF.Functions.ILike(d.Description, pattern)))
            .Take(topK);

        var results = await query.ToListAsync();
        return results.Select(x => (x, 0.0)).ToList();
    }
}
