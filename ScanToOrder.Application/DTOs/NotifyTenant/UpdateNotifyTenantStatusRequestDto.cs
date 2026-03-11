using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.NotifyTenant
{
    public class UpdateNotifyTenantStatusRequestDto
    {
        public List<int> NotificationIds { get; set; } = new List<int>();
        public DateTime ReadAt { get; set; } = DateTime.UtcNow;
        public NotifyTenantStatus Status { get; set; } = NotifyTenantStatus.Read;
    }
}
