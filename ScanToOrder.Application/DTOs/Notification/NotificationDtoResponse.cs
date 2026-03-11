namespace ScanToOrder.Application.DTOs.Notification
{
    public class NotificationDtoResponse
    {
        public int NotificationId { get; set; }
        public string NotifyTitle { get; set; } = string.Empty;
        public string NotifySub { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
