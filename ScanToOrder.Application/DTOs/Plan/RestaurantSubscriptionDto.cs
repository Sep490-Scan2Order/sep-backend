using System;

namespace ScanToOrder.Application.DTOs.Plan;

public class RestaurantSubscriptionDto
{
    public int RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public int? CurrentSubscriptionId { get; set; }
    public int? CurrentPlanId { get; set; }
    public string? CurrentPlanName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "None";
}
