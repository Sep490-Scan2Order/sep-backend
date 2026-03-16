namespace ScanToOrder.Application.Message
{
    public partial class SubscriptionMessage
    {
        public class SubscriptionError
        {
            public const string INVOICE_ZERO_CONTACT_ADMIN = "Hóa đơn 0đ, vui lòng liên hệ Admin để nâng cấp tự động.";
            public const string PAYMENT_SYSTEM_BUSY = "Hệ thống thanh toán đang bận, vui lòng thử lại sau.";
            public const string NO_PERMISSION_TO_UPDATE_TRANSACTION = "Không có quyền cập nhật giao dịch này.";
            public const string NO_PERMISSION_TO_VIEW_TRANSACTION = "Không có quyền xem giao dịch này.";
        }

        public class SubscriptionSuccess
        {
        }
    }
}
