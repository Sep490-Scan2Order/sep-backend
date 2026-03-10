using System.ComponentModel.DataAnnotations;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.Plan;

public class PlanCheckoutItemRequest
{
    [Required]
    public int RestaurantId { get; set; }

    [Required]
    public int TargetPlanId { get; set; }

    [Required]
    public BillingCycle Cycle { get; set; }

    [Required]
    [Range(1, 100, ErrorMessage = "Số lượng chu kỳ phải từ 1 trở lên.")]
    public int Quantity { get; set; }
}