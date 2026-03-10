using System.ComponentModel.DataAnnotations;

namespace ScanToOrder.Application.DTOs.Plan;

public class PlanCheckoutRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Phải chọn ít nhất 1 nhà hàng để thanh toán.")]
    public List<PlanCheckoutItemRequest> Items { get; set; } = new();
}