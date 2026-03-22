using ScanToOrder.Application.DTOs.Search;

namespace ScanToOrder.Application.Interfaces;

public interface IHybridSearchService
{
    Task<List<HybridSearchResponse>> SearchAsync(HybridSearchRequest request);
}
