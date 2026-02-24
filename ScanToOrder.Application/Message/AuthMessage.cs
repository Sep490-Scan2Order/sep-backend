namespace ScanToOrder.Application.Message
{
    public partial class AuthMessage
    {
        public class AuthSuccess
        {
            public const string LOGIN_SUCCESS = "Đăng nhập thành công.";
            public const string LOGOUT_SUCCESS = "Đăng xuất thành công.";
            public const string REFRESH_TOKEN_SUCCESS = "Làm mới token thành công.";
        }

        public class AuthError
        {
            public const string INVALID_CREDENTIALS = "Thông tin đăng nhập không chính xác.";
            public const string ACCOUNT_LOCKED = "Tài khoản đã bị khóa. Vui lòng liên hệ hỗ trợ để biết thêm chi tiết.";
            public const string TOKEN_EXPIRED = "Token đã hết hạn. Vui lòng đăng nhập lại.";
            public const string TOKEN_INVALID = "Token không hợp lệ. Vui lòng đăng nhập lại.";
            public const string ACCOUNT_NOT_FOUND = "Tài khoản chưa được đăng ký.";
            public const string ACCOUNT_NO_PASSWORD = "Tài khoản chưa đặt mật khẩu. Vui lòng đăng ký lại với mật khẩu.";
            public const string ACCOUNT_WRONG_PASSWORD_PHONE = "Số điện thoại hoặc mật khẩu không đúng.";
            public const string ACCOUNT_WRONG_PASSWORD = "Mật khẩu không đúng.";
            public const string PHONE_REGISTERED = "Số điện thoại này đã được đăng ký.";
        }
    }
}
