using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{

    public class StaffController : BaseController
    {
        private readonly IStaffService _staffService;

        public StaffController(IStaffService staffService)
        {
            _staffService = staffService;
        }

        [HttpPost("create-staff")]
        public async Task<ActionResult<ApiResponse<StaffDto>>> AddStaff([FromBody] CreateStaffRequest request)
        {
            var result = await _staffService.CreateStaff(request);
            return Success(result);
        }

        [HttpGet ("get-all")]
        public async Task<IActionResult> GetAllStaff(int restaurantId, int page = 1, int pageSize = 10)
        {
            var result = await _staffService.GetAllStaff(restaurantId, page, pageSize);
            return Ok(result);
        }

        [HttpGet("available-cashiers")]
        public async Task<IActionResult> GetAvailableCashiers()
        {
            var result = await _staffService.GetAvailableCashiers();
            return Ok(result);
        }

        [HttpGet("restaurant/{restaurantId}")]
        public async Task<ActionResult<ApiResponse<List<StaffDto>>>> GetStaffByRestaurant(int restaurantId)
        {
            var result = await _staffService.GetStaffByRestaurant(restaurantId);
            return Success(result);
        }
    }
}
