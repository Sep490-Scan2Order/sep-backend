using System.ComponentModel.DataAnnotations;
using ScanToOrder.Domain.Enums;

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

        [Required]
        [Range(1, int.MaxValue)]
        public int DurationInDays { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int Level { get; set; }

        [Required]
        public PlanStatus Status { get; set; }

        public bool IsTrial { get; set; } = false;
        public bool IsCommissionExempt { get; set; } = false;

        public UpdatePlanFeaturesRequest Features { get; set; } = new UpdatePlanFeaturesRequest();
    }

    public class UpdatePlanFeaturesRequest
    {
        public bool CanUseAIUpsell { get; set; } = false;
        public bool CanRecommendationOnTop { get; set; } = false;
        public bool CanUsePromotions { get; set; } = false;
        public bool CanCustomMenuTemplate { get; set; } = false;
    }
}
