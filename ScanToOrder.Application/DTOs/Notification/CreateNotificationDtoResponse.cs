namespace ScanToOrder.Application.DTOs.Notification
{
    public class CreateNotificationDtoResponse
    {
        public int Id { get; set; }
        public string NotifyTitle { get; set; } = string.Empty;
        public string NotifySub { get; set; } = string.Empty;
        public string? SystemBlogUrl { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
