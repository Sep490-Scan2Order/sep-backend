namespace ScanToOrder.Application.Message
{
    public partial class EmailMessage
    {
        public class EmailError
        {
            public const string EMAIL_FAILED = "Gửi email thất bại. Vui lòng thử lại sau.";
            public const string TEMPLATE_NOT_FOUND = "Không tìm thấy mẫu email yêu cầu.";
            public const string EMAIL_NOT_NULL = "Địa chỉ email không được để trống.";
        }

        public class EmailSuccess
        {
            public const string EMAIL_SENT = "Email đã được gửi thành công.";
            public const string EMAIL_SENT_VIA_TEMPLATE = "Email đã được gửi thành công từ template.";
        }
    }
}