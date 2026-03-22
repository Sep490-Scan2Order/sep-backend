namespace ScanToOrder.Application.DTOs.Search;

public class HybridSearchRequest
{
    public string Keyword { get; set; } = null!;
    // Location for GPS-based reranking (Optional)
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    // Radius in km
    public double RadiusKm { get; set; } = 5;
    // Limit per Search type
    public int TopK { get; set; } = 10;
}
