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

        [HttpPost]
        public async Task<ActionResult<ApiResponse<StaffDto>>> AddStaff([FromBody] CreateStaffRequest request)
        {
            var result = await _staffService.CreateStaff(request);
            return Success(result);
        }
    }
}
