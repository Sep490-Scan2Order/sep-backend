namespace ScanToOrder.Application.Message
{
    public partial class TenantMessage
    {
        public class TenantSuccess
        {
            public const string TENANT_REGISTERED = "Đăng ký thành công.";
            public const string TENANT_UPDATED = "Cập nhật thông tin thành công.";
            public const string TENANT_RESET_PASSWORD = "Mật khẩu đã được đặt lại thành công.";
        }

        public class TenantError
        {
            public const string TENANT_ALREADY_EXISTS = "Tài khoản đã tồn tại.";
            public const string TENANT_NOT_FOUND = "Không tìm thấy tài khoản.";
            public const string TAX_CODE_INVALID = "Mã số thuế không hợp lệ.";
            public const string TAX_CODE_ALREADY_EXISTS = "Mã số thuế đã tồn tại.";
            public const string TENANT_LIMIT_RESTAURANTS = "Bạn đã đạt giới hạn số lượng nhà hàng của gói hiện tại.";
            public const string TENANT_MISSING_TAX_NUMBER = "Vui lòng cung cấp mã số thuế hoặc thông tin tài chính để tạo nhà hàng.";
            public const string TENANT_MISSING_BANK = "Vui lòng cung cấp thông tin ngân hàng để tạo nhà hàng.";
            public const string TENANT_MISSING_CARD = "Vui lòng cung cấp thông tin thẻ ngân hàng để tạo nhà hàng.";
            public const string TENANT_MISSING_PHONE = "Vui lòng cung cấp số điện thoại để tạo nhà hàng.";
        }
    }
}
