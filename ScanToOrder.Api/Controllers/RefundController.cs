using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RefundController : BaseController
    {
        private readonly IRefundService _refundService;

        public RefundController(IRefundService refundService)
        {
            _refundService = refundService;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<bool>>> RefundOrder([FromForm] RefundRequest request)
        {
            var result = await _refundService.RefundOrderAsync(request);

            return Success(result, "Hoàn tiền thành công.");

        }
        [HttpPost("confirm-system-payment")]
        public async Task<ActionResult<ApiResponse<bool>>> ConfirmSystemPayment([FromForm] ConfirmSystemPaymentRequest request)
        {
            var result = await _refundService.ConfirmSystemErrorPaymentAsync(request);
            return Success(result, "Xác nhận thanh toán hệ thống thành công.");
        }
    }
}