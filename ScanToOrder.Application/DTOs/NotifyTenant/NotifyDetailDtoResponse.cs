using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.NotifyTenant
{
    public class NotifyDetailDtoResponse
    {
        public int NotificationId { get; set; }
        public string NotifyTitle { get; set; } = string.Empty;
        public string NotifySub { get; set; } = string.Empty;
        public string SystemBlogUrl { get; set; } = string.Empty;
        public NotifyTenantStatus Status { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
