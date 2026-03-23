using ScanToOrder.Application.DTOs.Search;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Interfaces;
using Pgvector;
using System.Linq;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Restaurants;
using Microsoft.Extensions.DependencyInjection;

namespace ScanToOrder.Application.Services;

public class HybridSearchService : IHybridSearchService
{
    private readonly IOpenAiService _openAiService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServiceScopeFactory _scopeFactory;

    public HybridSearchService(IOpenAiService openAiService, IUnitOfWork unitOfWork, IServiceScopeFactory scopeFactory)
    {
        _openAiService = openAiService;
        _unitOfWork = unitOfWork;
        _scopeFactory = scopeFactory;
    }

    public async Task<List<HybridSearchResponse>> SearchAsync(HybridSearchRequest request)
    {
        // 1. Get embedding for the keyword
        float[]? rawVector = null;
        try
        {
            rawVector = await _openAiService.GetEmbeddingAsync(request.Keyword);
        }
        catch
        {
            /* Fallback to keyword-only if API fails */
        }

        // 2. Execute searches in parallel using scoped DbContexts for maximum performance
        var keywordResTask = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISearchRepository>();
            return await repo.SearchRestaurantsByKeywordAsync(request.Keyword, request.TopK);
        });

        var keywordDishTask = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISearchRepository>();
            return await repo.SearchDishesByKeywordAsync(request.Keyword, request.TopK);
        });

        Task<List<(Restaurant, double)>> vectorResTask = Task.FromResult(new List<(Restaurant, double)>());
        Task<List<(Dish, double)>> vectorDishTask = Task.FromResult(new List<(Dish, double)>());

        if (rawVector != null)
        {
            var vector = new Vector(rawVector);
            vectorResTask = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<ISearchRepository>();
                return await repo.SearchRestaurantsByVectorAsync(vector, request.TopK);
            });
            vectorDishTask = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<ISearchRepository>();
                return await repo.SearchDishesByVectorAsync(vector, request.TopK);
            });
        }

        await Task.WhenAll(keywordResTask, keywordDishTask, vectorResTask, vectorDishTask);

        var keywordResResult = keywordResTask.Result;
        var keywordDishResult = keywordDishTask.Result;
        var vectorResResult = vectorResTask.Result;
        var vectorDishResult = vectorDishTask.Result;

        // 3. Merge and deduplicate
        var allRestaurants = new Dictionary<int, HybridSearchResponse>();

        void ProcessRestaurants(IEnumerable<(Restaurant R, double Dist)> items, double weight)
        {
            foreach (var item in items)
            {
                if (!allRestaurants.ContainsKey(item.R.Id))
                {
                    allRestaurants[item.R.Id] = new HybridSearchResponse
                    {
                        RestaurantId = item.R.Id,
                        RestaurantName = item.R.RestaurantName,
                        Description = item.R.Description ?? string.Empty,
                        ImageUrl = item.R.Image,
                        BackgroundImageUrl = item.R.ProfileUrl,
                        FinalScore = 0
                    };
                }

                double relevance = (2.0 - item.Dist) * weight;
                if (relevance > allRestaurants[item.R.Id].FinalScore)
                    allRestaurants[item.R.Id].FinalScore = relevance;
            }
        }

        double maxAllowedDistance = 0.75;

        ProcessRestaurants(keywordResResult, 1.2); // Keyword exact matches get priority
        ProcessRestaurants(vectorResResult.Where(x => x.Item2 < maxAllowedDistance), 1.0);

        // 4. Merge Dishes, find parent restaurants
        var allDishes = keywordDishResult.Select(x => (x.Item1, x.Item2, Weight: 1.2))
            .Concat(vectorDishResult
                .Where(x => x.Item2 < maxAllowedDistance)
                .Select(x => (x.Item1, x.Item2, Weight: 1.0)))
            .GroupBy(x => x.Item1.Id)
            .Select(g => g.OrderByDescending(x => (2.0 - x.Item2) * x.Weight).First())
            .ToList();

        var tenantIds = allDishes.Select(x => x.Item1.Category.TenantId).Distinct().ToList();
        var tenantRestaurants =
            await _unitOfWork.Restaurants.GetAllAsync(r => tenantIds.Contains(r.TenantId) && r.IsActive == true);
        var tenantToRestaurantMap =
            tenantRestaurants.GroupBy(r => r.TenantId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var dishItem in allDishes)
        {
            var dish = dishItem.Item1;
            double relevance = (2.0 - dishItem.Item2) * dishItem.Weight;

            if (tenantToRestaurantMap.TryGetValue(dish.Category.TenantId, out var restaurantsForDish))
            {
                foreach (var r in restaurantsForDish)
                {
                    double restaurantRelevanceFromDish = relevance * 0.8;
                    if (!allRestaurants.TryGetValue(r.Id, out var resDto))
                    {
                        resDto = new HybridSearchResponse
                        {
                            RestaurantId = r.Id,
                            RestaurantName = r.RestaurantName,
                            Description = r.Description ?? string.Empty,
                            ImageUrl = r.Image,
                            BackgroundImageUrl = r.ProfileUrl,
                            FinalScore = restaurantRelevanceFromDish // penalty for matching via dish instead of direct
                        };
                        allRestaurants[r.Id] = resDto;
                    }

                    if (resDto.FinalScore < restaurantRelevanceFromDish)
                        resDto.FinalScore = restaurantRelevanceFromDish;

                    if (!resDto.SuggestedDishes.Any(d => d.DishId == dish.Id))
                    {
                        resDto.SuggestedDishes.Add(new HybridSearchDishDto
                        {
                            DishId = dish.Id,
                            DishName = dish.DishName,
                            Description = dish.Description ?? string.Empty,
                            Price = dish.Price,
                            ImageUrl = dish.ImageUrl,
                            RelevanceScore = relevance,
                            SemanticDistance = dishItem.Item2
                        });
                    }
                }
            }
        }

        var finalResults = allRestaurants.Values.ToList();

        // 5. Compute GPS Distance and Rerank
        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            var resIds = finalResults.Select(x => x.RestaurantId).ToList();
            var resLocations = await _unitOfWork.Restaurants.GetAllAsync(r => resIds.Contains(r.Id));

            foreach (var fr in finalResults)
            {
                var locRes = resLocations.FirstOrDefault(r => r.Id == fr.RestaurantId);
                if (locRes?.Location != null)
                {
                    double rLat = locRes.Location.Coordinate.Y;
                    double rLon = locRes.Location.Coordinate.X;
                    fr.GpsDistanceKm = CalculateDistanceKm(request.Latitude.Value, request.Longitude.Value, rLat, rLon);

                    if (fr.GpsDistanceKm.Value > request.RadiusKm)
                        fr.FinalScore *= 0.1;
                    else
                        fr.FinalScore *= (1.0 - (fr.GpsDistanceKm.Value / request.RadiusKm) * 0.5);
                }
            }
        }

        // Sort dishes within restaurant by relevance
        foreach (var res in finalResults)
        {
            res.SuggestedDishes = res.SuggestedDishes.OrderByDescending(d => d.RelevanceScore).Take(5).ToList();
        }

        return finalResults
            .OrderByDescending(r => r.FinalScore)
            .Take(request.TopK)
            .ToList();
    }

    private double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}   