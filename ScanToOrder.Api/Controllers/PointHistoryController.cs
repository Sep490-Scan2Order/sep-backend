using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.PointHistory;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class PointHistoryController : BaseController
    {
        private readonly IPointHistoryService _pointHistoryService;
        public PointHistoryController(IPointHistoryService pointHistoryService)
        {
            _pointHistoryService = pointHistoryService;
        }

        [HttpPost("plus-point-history")]
        public async Task<ActionResult<ApiResponse<AddPointHistoryDtoResponse>>> PlusPointHistory([FromBody] AddPointHistoryDtoRequest pointHistoryDto)
        {
            var result = await _pointHistoryService.PlusPointHistoryAsync(pointHistoryDto);
            return Success(result);
        }
    }
}
