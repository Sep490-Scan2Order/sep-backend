using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    [Route("api/[controller]")]
    public class PlanController : BaseController
    {
        private readonly IPlanService _planService;

        public PlanController(IPlanService planService)
        {
            _planService = planService;
        }
        
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<PlanResponse>>>> GetAllPlans()
        {
            var result = await _planService.GetAllPlansAsync();
            return Success(result);
        }


        [HttpGet("{id:int}")]
        //[Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PlanResponse>>> GetPlanById(int id)
        {
            var result = await _planService.GetPlanByIdAsync(id);
            return Success(result);
        }
        
        [HttpPost]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request)
        {
            var result = await _planService.CreatePlanAsync(request);
            return CreatedSuccess(nameof(GetPlanById), new { id = result.Id }, result, "Tạo gói dịch vụ thành công.");
        }
        
        [HttpPut("{id:int}")]
        //[Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PlanResponse>>> UpdatePlan(int id, [FromBody] UpdatePlanRequest request)
        {
            var result = await _planService.UpdatePlanAsync(id, request);
            return Success(result, "Cập nhật gói dịch vụ thành công.");
        }
    }
}
