namespace ScanToOrder.Application.Message
{
    public partial class TenantMessage
    {
        public class TenantSuccess
        {
            public const string TENANT_REGISTERED = "Đăng ký thành công.";
        }

        public class TenantError
        {
            public const string TENANT_ALREADY_EXISTS = "Tài khoản đã tồn tại.";
            public const string TENANT_NOT_FOUND = "Không tìm thấy tài khoản.";
        }
    }
}
