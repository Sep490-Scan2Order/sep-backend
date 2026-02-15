namespace ScanToOrder.Application.DTOs.Notification
{
    public class CreateNotificationDtoRequest
    {
        public string NotifyTitle { get; set; } = string.Empty;
        public string NotifySub { get; set; } = string.Empty;
    }
}
