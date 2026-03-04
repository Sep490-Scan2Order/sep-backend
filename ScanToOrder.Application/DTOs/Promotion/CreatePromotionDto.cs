using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.Promotion;

public class CreatePromotionDto
{
    public string Name { get; set; } = null!;
    public PromotionType Type { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountValue { get; set; }
    public decimal MinOrderValue { get; set; }
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public TimeSpan? DailyStartTime { get; set; }
    public TimeSpan? DailyEndTime { get; set; }
    public DaysOfWeek DaysOfWeek { get; set; } = DaysOfWeek.None;
    
    public bool IsGlobal { get; set; }
    public int? Priority { get; set; }

    public List<int>? DishIds { get; set; } 
    public List<int>? RestaurantIds { get; set; }
}