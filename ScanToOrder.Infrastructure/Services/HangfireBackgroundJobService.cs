using Hangfire;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Infrastructure.Services;

public class HangfireBackgroundJobService : IBackgroundJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireBackgroundJobService(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void EnqueueSearchIndexDish(int dishId)
    {
        _backgroundJobClient.Enqueue<ISearchIndexService>(x => x.IndexDishAsync(dishId));
    }

    public void EnqueueSearchIndexRestaurant(int restaurantId)
    {
        _backgroundJobClient.Enqueue<ISearchIndexService>(x => x.IndexRestaurantAsync(restaurantId));
    }

    public void EnqueueFullReIndex()
    {
        _backgroundJobClient.Enqueue<ISearchIndexService>(x => x.FullReIndexAsync());
    }
}
