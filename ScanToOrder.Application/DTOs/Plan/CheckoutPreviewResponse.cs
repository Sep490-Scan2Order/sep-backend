namespace ScanToOrder.Application.DTOs.Plan;

public class CheckoutPreviewResponse
{
    public decimal TotalAmountToPay { get; set; }
    public List<CheckoutPreviewItemResponse> Details { get; set; } = new();
}