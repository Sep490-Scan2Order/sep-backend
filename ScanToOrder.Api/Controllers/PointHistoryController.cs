using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.PointHistory;
using ScanToOrder.Application.Interfaces;

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
        public async Task<IActionResult> PlusPointHistory([FromBody] AddPointHistoryDtoRequest pointHistoryDto)
        {
            try
            {
                var result = await _pointHistoryService.PlusPointHistoryAsync(pointHistoryDto);
                return Success(result, "Thêm lịch sử điểm cộng thành công.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
