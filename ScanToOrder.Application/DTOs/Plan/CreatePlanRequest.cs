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

        [Required]
        [Range(1, int.MaxValue)]
        public int DurationInDays { get; set; } = 30;

        [Required]
        [Range(0, int.MaxValue)]
        public int Level { get; set; }

        public CreatePlanFeaturesRequest Features { get; set; } = new CreatePlanFeaturesRequest();
    }

    public class CreatePlanFeaturesRequest
    {
        public bool CanUseAIUpsell { get; set; } = false;
        public bool CanRecommendationOnTop { get; set; } = false;
        public bool CanUsePromotions { get; set; } = false;
        public bool CanCustomMenuTemplate { get; set; } = false;
    }
}
