namespace ScanToOrder.Application.DTOs.Plan
{
    public class PlanResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    
        public decimal MonthlyPrice { get; set; }
        public decimal YearlyPrice { get; set; }
    
        public decimal DailyRateMonth { get; set; }
        public decimal DailyRateYear { get; set; }
    
        public int Level { get; set; }
        public string Status { get; set; } = null!;
    
        public PlanFeaturesResponse Features { get; set; } = new PlanFeaturesResponse();
    }
}
