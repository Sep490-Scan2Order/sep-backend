using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using System.Threading.Tasks;

namespace ScanToOrder.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AddOnController : BaseController
    {
        private readonly IAddOnService _addOnService;

        public AddOnController(IAddOnService addOnService)
        {
            _addOnService = addOnService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<AddOnDto>>>> GetAll()
        {
            var result = await _addOnService.GetAllAddOns();
            return Success(result); 
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<AddOnDto>>> Create([FromBody] CreateAddOnRequest request)
        {
            var result = await _addOnService.CreateAddOn(request);
            return Success(result);
        }
    }
}
