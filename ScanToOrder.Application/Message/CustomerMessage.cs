namespace ScanToOrder.Application.Message
{
    public partial class CustomerMessage
    {
        public class CustomerSuccess
        {
            public const string CUSTOMER_RETRIEVED = "Lấy được thông tin khách hàng thành công!";
            public const string CUSTOMER_CREATED = "Khách hàng đã tạo tài khoản thành công!";
            public const string CUSTOMER_UPDATED = "Khách hàng đã cập nhật thông tin thành công!";
            public const string CUSTOMER_DELETED = "Khách hàng đã xóa tài khoản thành công!";
        }

        public class CustomerError
        {
            public const string CUSTOMER_NOT_FOUND = "Không tìm thấy thông tin của khách hàng!";
            public const string CUSTOMER_ALREADY_EXISTS = "Thông tin của khách hàng đã tồn tại trong hệ thống.";
            public const string INVALID_CUSTOMER_DATA = "Dữ liệu của khách hàng không hợp lệ";
        }
    }
}
