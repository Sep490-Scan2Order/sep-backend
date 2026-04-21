using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.DTOs.Shift;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class ShiftController : BaseController
    {
        private readonly IShiftService _shiftService;
        private readonly IAuthenticatedUserService _authenticatedUserService;

        public ShiftController(IShiftService shiftService, IAuthenticatedUserService authenticatedUserService)
        {
            _shiftService = shiftService;
            _authenticatedUserService = authenticatedUserService;
        }

        [HttpPost("check-in")]
        [Authorize(Roles = "Cashier, Staff")]
        public async Task<ActionResult<ApiResponse<ShiftDto>>> CheckIn([FromBody] CheckInShiftRequest request)
        {

            var result = await _shiftService.CheckInShiftAsync(
                request.RestaurantId,
                request.StaffId,
                request.Note
            );

            return Success(result);
        }

        [HttpPost("check-out")]
        [Authorize(Roles = "Cashier, Staff")]
        public async Task<ActionResult<ApiResponse<ShiftDto>>> CheckOut([FromBody] CheckOutShiftRequest request)
        {
            var result = await _shiftService.CheckOutShiftAsync(
                request.ShiftId,
                request.Note
            );

            return Success(result);
        }
        [HttpGet("{shiftId}/report")]
        [Authorize(Roles = "Admin, Tenant, Cashier")]
        public async Task<ActionResult<ApiResponse<ShiftReportDto>>> GetShiftReport([FromRoute] int shiftId)
        {
            var result = await _shiftService.GetShiftReportAsync(shiftId);
            return Success(result);
        }

        [HttpGet("{shiftId}/preview")]
        [Authorize(Roles = " Cashier")]
        public async Task<ActionResult<ApiResponse<ShiftReportDto>>> GetShiftPreview([FromRoute] int shiftId)
        {
            var result = await _shiftService.GetShiftPreviewAsync(shiftId);
            return Success(result);
        }

        [HttpGet("reports")]
        [Authorize(Roles = "Admin, Tenant, Cashier")]
        public async Task<ActionResult<ApiResponse<PagedResult<ShiftReportDto>>>> GetAllShiftReports(
            [FromQuery] int restaurantId,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var result = await _shiftService.GetAllShiftReportsAsync(restaurantId, pageIndex, pageSize, from, to);
            return Success(result);
        }

        [HttpGet("reports/staff/{staffId}")]
        [Authorize(Roles = "Admin, Tenant, Cashier")]
        public async Task<ActionResult<ApiResponse<PagedResult<ShiftReportDto>>>> GetShiftReportsByStaff(
            [FromRoute] Guid staffId,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _shiftService.GetShiftReportsByStaffAsync(staffId, pageIndex, pageSize);
            return Success(result);
        }

        [HttpGet("current")]
        [Authorize(Roles = "Cashier, Staff ")]
        public async Task<ActionResult<ApiResponse<ShiftDto>>> GetCurrentShift()
        {
            var staffId = _authenticatedUserService.ProfileId.Value;
            var result = await _shiftService.GetShiftByIdAsync(staffId);
            return Success(result);
        }

        [HttpPost("{shiftId}/block")]
        [Authorize(Roles = "Cashier")]
        public async Task<ActionResult<ApiResponse<string>>> BlockStaffShift([FromRoute] int shiftId, [FromBody] BlockShiftRequest request)
        {
            await _shiftService.BlockStaffShiftAsync(shiftId, request.Reason);
            return Success("Đã chặn ca làm việc thành công.");
        }

        [HttpGet("{cashierShiftId}/staff-shifts")]
        [Authorize(Roles = "Cashier")]
        public async Task<ActionResult<ApiResponse<IEnumerable<ShiftDto>>>> GetStaffShifts([FromRoute] int cashierShiftId)
        {
            var result = await _shiftService.GetStaffShiftsByCashierShiftIdAsync(cashierShiftId);
            return Success(result);
        }
        [HttpGet("{shiftId}/transfer-qr")]
        [Authorize(Roles = "Cashier")]
        public async Task<ActionResult<ApiResponse<ShiftTransferQrResponse>>> GetTransferQr([FromRoute] int shiftId)
        {
            var result = await _shiftService.GetTransferQrAsync(shiftId);
            return Success(result);
        }

        [HttpGet("current/pending-report")]
        [Authorize(Roles = "Cashier, Staff")]
        public async Task<ActionResult<ApiResponse<ShiftReportDto>>> GetPendingShiftReport()
        {
            var staffId = _authenticatedUserService.ProfileId.Value;
            var result = await _shiftService.GetPendingShiftReportAsync(staffId);
            return Success(result);
        }
    }
}
