using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Search;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers;

public class SearchController : BaseController
{
    private readonly IHybridSearchService _hybridSearchService;

    public SearchController(IHybridSearchService hybridSearchService)
    {
        _hybridSearchService = hybridSearchService;
    }

    [HttpGet("hybrid")]
    public async Task<ActionResult<ApiResponse<List<HybridSearchResponse>>>> HybridSearch([FromQuery] HybridSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Keyword))
            return BadRequest("Keyword cannot be empty.");

        var results = await _hybridSearchService.SearchAsync(request);
        return Success(results, "Search completed successfully.");
    }

    [HttpPost("reindex-all")]
    public ActionResult<ApiResponse<string>> ReindexAll([FromServices] IBackgroundJobService backgroundJobService)
    {
        backgroundJobService.EnqueueFullReIndex();
        return Success("Job đồng bộ dữ liệu (Re-index Vector) cho toàn bộ Nhà hàng & Món ăn đã được thêm vào hàng đợi Hangfire thành công.", "Queued");
    }
}
