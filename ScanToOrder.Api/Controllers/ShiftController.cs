using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [Authorize(Roles = "Cashier")]
        public async Task<ActionResult<ApiResponse<ShiftDto>>> CheckIn([FromBody] CheckInShiftRequest request)
        { 

            var result = await _shiftService.CheckInShiftAsync(
                request.RestaurantId,
                request.StaffId,
                request.OpeningCashAmount,
                request.Note
            );

            return Success(result);
        }

        [HttpPost("check-out")]
        [Authorize(Roles = "Cashier")]
        public async Task<ActionResult<ApiResponse<ShiftDto>>> CheckOut([FromBody] CheckOutShiftRequest request)
        {
            var result = await _shiftService.CheckOutShiftAsync(
                request.ShiftId,
                request.CashAmount,
                request.Note
            );

            return Success(result);
        }
    }
}
