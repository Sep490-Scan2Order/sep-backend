using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace ScanToOrder.Application.DTOs.Plan
{
    public class CreatePlanRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal MonthlyPrice { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal YearlyPrice { get; set; }

        public int DurationInDays { get; set; } = 30; // Default or ignored if based on monthly/yearly

        public int Level { get; set; } = 0;

        public PlanStatus Status { get; set; } = PlanStatus.Active;

        public PlanFeaturesConfig Features { get; set; } = new PlanFeaturesConfig();
    }
}
