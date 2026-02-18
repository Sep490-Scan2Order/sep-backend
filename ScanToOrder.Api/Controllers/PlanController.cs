using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class PlanController : BaseController
    {
        private readonly IPlanService _planService;

        public PlanController(IPlanService planService)
        {
            _planService = planService;
        }


        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<PlanDto>>>> GetAllPlan()
        {
            var result = await _planService.GetAllPlansAsync();
            return Success(result);
        }
    }
}
