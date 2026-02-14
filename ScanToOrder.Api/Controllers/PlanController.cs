using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;

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
        public async Task<IActionResult> GetAllPlan()
        {
            var result = await _planService.GetAllPlansAsync();
            return Success(result);
        }
    }
}
