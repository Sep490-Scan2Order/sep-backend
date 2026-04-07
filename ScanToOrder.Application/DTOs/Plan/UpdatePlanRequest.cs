using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace ScanToOrder.Application.DTOs.Plan
{
    public class UpdatePlanRequest
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

        public int DurationInDays { get; set; } = 30;

        public int Level { get; set; }

        public PlanStatus Status { get; set; }

        public PlanFeaturesConfig Features { get; set; } = new PlanFeaturesConfig();
    }
}
