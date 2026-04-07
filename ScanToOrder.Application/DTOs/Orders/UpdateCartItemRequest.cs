namespace ScanToOrder.Application.DTOs.Orders;

public class UpdateCartItemRequest
{
    public string CartId { get; set; } = string.Empty;

    public int DishId { get; set; }
    public int NewQuantity { get; set; }
}
