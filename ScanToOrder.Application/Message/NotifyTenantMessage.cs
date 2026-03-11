namespace ScanToOrder.Application.Message
{
    public partial class NotifyTenantMessage
    {
        public class NotifyTenantSuccess
        {
            public const string ALL_NOTIFY_TENANT_READED = "Bạn đã đọc tất cả thông báo!";
        }

        public class NotifyTenantError
        {
            public const string NOTIFY_TENANT_NOT_FOUND = "Không tìm thấy thông báo này!";
        }
    }
}
