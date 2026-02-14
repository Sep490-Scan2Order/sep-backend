using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.MemberPoint;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Api.Controllers
{
    public class MemberPointController : BaseController
    {
        private readonly IMemberPointService _memberPointService;
        public MemberPointController(IMemberPointService memberPointService)
        {
            _memberPointService = memberPointService;
        }

        [HttpPost("add-member-point")]
        public async Task<IActionResult> AddMemberPoint([FromBody] AddMemberPointDtoRequest memberPointDto)
        {
            try
            {
                var result = await _memberPointService.AddMemberPointAsync(memberPointDto);
                return Success(result, "Thêm điểm thành viên thành công.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
