namespace ScanToOrder.Application.DTOs.Search;

public class HybridSearchResponse
{
    public int RestaurantId { get; set; }
    public string RestaurantName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    
    public double? GpsDistanceKm { get; set; }
    public double FinalScore { get; set; } // Max score combination of dish scores + location
    
    public List<HybridSearchDishDto> SuggestedDishes { get; set; } = new();
}
