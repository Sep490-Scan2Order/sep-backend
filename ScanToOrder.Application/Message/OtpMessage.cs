namespace ScanToOrder.Application.Message
{
    public class OtpMessage
    {
        public const string OTP_GENERATED = "OTP generated and sent to email.";
        public const string OTP_VALIDATED = "OTP validated successfully.";
        public const string OTP_REGISTER = "Register";
        public const string OTP_RESET_PASSWORD = "ResetPassword";
        public const string OTP_FORGOT_PASSWORD = "ForgotPassword";
    }
}
