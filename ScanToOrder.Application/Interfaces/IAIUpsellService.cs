namespace ScanToOrder.Application.Interfaces
{
    public interface IAIUpsellService
    {
        Task<(List<int> DishIds, string Source)> GetRecommendationsAsync(
            int restaurantId,
            List<int> cartDishIds,
            int top = 3);
    }
}
