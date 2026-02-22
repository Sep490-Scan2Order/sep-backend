namespace ScanToOrder.Application.DTOs.Notification
{
    public class CreateNotifyTenantDtoResponse
    {
        public int Id { get; set; }
        public int NotificationId { get; set; }
        public Guid TenantId { get; set; }
    }
}
