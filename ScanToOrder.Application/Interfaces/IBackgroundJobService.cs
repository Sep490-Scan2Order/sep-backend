namespace ScanToOrder.Application.Interfaces;

public interface IBackgroundJobService
{
    void EnqueueSearchIndexDish(int dishId);
    void EnqueueSearchIndexRestaurant(int restaurantId);
    void EnqueueFullReIndex();
}
