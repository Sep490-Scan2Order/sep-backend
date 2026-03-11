namespace ScanToOrder.Application.DTOs.Notification
{
    public class CreateNotifyTenantDtoRequest
    {
        public int NotificationId { get; set; }
        public List<Guid> TenantIds { get; set; }
    }
}
