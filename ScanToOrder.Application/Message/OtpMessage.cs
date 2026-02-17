namespace ScanToOrder.Application.Message
{
    public partial class OtpMessage
    {
        public class OtpSuccess
        {
            public const string OTP_GENERATED = "Mã OTP đã được tạo và gửi đến email của bạn.";
            public const string OTP_VALIDATED = "Xác thực mã OTP thành công.";
        }

        public class OtpKeyword
        {
            public const string OTP_REGISTER = "Register";
            public const string OTP_RESET_PASSWORD = "ResetPassword";
            public const string OTP_FORGOT_PASSWORD = "ForgotPassword";
        }

        public class OtpError
        {
            public const string OTP_UNKNOWN = "Không tìm thấy mã OTP hoặc mã đã hết hạn.";
            public const string OTP_EXPIRED = "Mã OTP đã hết hạn hoặc không tồn tại.";
            public const string OTP_INVALID = "Mã OTP không chính xác. Vui lòng kiểm tra lại.";
        }
    }
}