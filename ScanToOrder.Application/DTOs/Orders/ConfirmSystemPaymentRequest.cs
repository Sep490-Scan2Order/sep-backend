using Microsoft.AspNetCore.Http;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class ConfirmSystemPaymentRequest
    {
        public Guid OrderId { get; set; }
        public IFormFile? ImageFile { get; set; }
        public Guid ResponsibleStaffId { get; set; }
        public string? Note { get; set; }
    }
}
