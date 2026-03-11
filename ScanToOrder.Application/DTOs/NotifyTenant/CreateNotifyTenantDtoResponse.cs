using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.NotifyTenant
{
    public class CreateNotifyTenantDtoResponse
    {
        public int Id { get; set; }
        public int NotificationId { get; set; }
        public Guid TenantId { get; set; }
        public NotifyTenantStatus Status { get; set; } = NotifyTenantStatus.Unread;
    }
}
