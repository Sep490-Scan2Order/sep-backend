namespace ScanToOrder.Application.DTOs.Orders;

public class GetAvailablePromotionsRequest
{
    public int RestaurantId { get; set; }
    public decimal OrderTotal { get; set; }
}
