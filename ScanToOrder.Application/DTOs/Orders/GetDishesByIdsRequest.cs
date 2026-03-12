namespace ScanToOrder.Application.DTOs.Orders;

public class GetDishesByIdsRequest
{
    public int RestaurantId { get; set; }
    public List<int> DishIds { get; set; } = new();
}
