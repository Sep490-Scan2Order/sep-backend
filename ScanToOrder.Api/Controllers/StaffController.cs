using Microsoft.AspNetCore.Authorization;
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

        [Authorize(Roles = "Admin, Tenant, Cashier")]
        [HttpPost("create-staff")]
        public async Task<ActionResult<ApiResponse<StaffDto>>> AddStaff([FromBody] CreateStaffRequest request)
        {
            var result = await _staffService.CreateStaff(request);
            return Success(result);
        }

        [Authorize(Roles = "Admin, Tenant, Cashier")]
        [HttpPut("update-staff/{staffId:guid}")]
        public async Task<ActionResult<ApiResponse<StaffDto>>> UpdateStaff(Guid staffId, [FromBody] UpdateStaffRequest request)
        {
            var result = await _staffService.UpdateStaff(staffId, request);
            return Success(result, "Cập nhật thông tin nhân viên thành công.");
        }

        [Authorize(Roles = "Admin, Tenant, Cashier")]
        [HttpGet ("get-all")]
        public async Task<IActionResult> GetAllStaff(int restaurantId, int page = 1, int pageSize = 10)
        {
            var result = await _staffService.GetAllStaff(restaurantId, page, pageSize);
            return Ok(result);
        }

        [Authorize(Roles = "Admin, Tenant, Cashier")]
        [HttpGet("available-cashiers")]
        public async Task<IActionResult> GetAvailableCashiers()
        {
            var result = await _staffService.GetAvailableCashiers();
            return Ok(result);
        }

        [Authorize(Roles = "Admin, Tenant, Cashier")]
        [HttpGet("restaurant")]
        public async Task<ActionResult<ApiResponse<List<StaffDto>>>> GetStaffByRestaurant(int restaurantId)
        {
            var result = await _staffService.GetStaffByRestaurant(restaurantId);
            return Success(result);
        }
    }
}
