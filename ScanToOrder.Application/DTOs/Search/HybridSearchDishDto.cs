namespace ScanToOrder.Application.DTOs.Search;

public class HybridSearchDishDto
{
    public int DishId { get; set; }
    public string DishName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = null!;
    public double RelevanceScore { get; set; }
    
    // Original Vector distance if matched via Semantic Search
    public double? SemanticDistance { get; set; }
}
