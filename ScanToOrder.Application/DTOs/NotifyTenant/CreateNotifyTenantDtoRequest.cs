namespace ScanToOrder.Application.DTOs.Notification
{
    public class CreateNotifyTenantDtoRequest
    {
        public int NotificationId { get; set; }
        public Guid TenantId { get; set; }
    }
}
