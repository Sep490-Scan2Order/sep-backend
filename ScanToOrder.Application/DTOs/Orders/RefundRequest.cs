using ScanToOrder.Domain.Enums;

using Microsoft.AspNetCore.Http;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class RefundRequest
    {
        public Guid OrderId { get; set; }
        public RefundType RefundType { get; set; }
        public Guid ResponsibleStaffId { get; set; }
        public string? Note { get; set; }
        public IFormFile? ImageFile { get; set; }
    }
}
