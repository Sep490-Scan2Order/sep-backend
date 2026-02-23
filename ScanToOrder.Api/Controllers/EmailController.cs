using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Email;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    public class EmailController : BaseController
    {
        private readonly IEmailService _emailService;
        public EmailController(IEmailService emailService)
        {
            _emailService = emailService;
        }
        [HttpPost("send")]
        public async Task<ActionResult<ApiResponse<bool>>> SendEmail([FromBody] SendEmailRequest request)
        {
            var result = await _emailService.SendEmailAsync(request.To, request.Subject, request.HtmlContent);
            return Success(result);
        }
    }
}
