using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Message
{
    public partial class ShiftMessage
    {
        public class ShiftError
        {
            public const string SHIFT_NOT_FOUND = "Không tìm thấy ca làm.";
            public const string SHIFT_ALREADY_OPEN = "Hiện có nhân viên đang làm trong ca.";
            public const string SHIFT_ALREADY_CLOSED = "Ca làm đã được đóng.";
            public const string OPENING_CASH_INVALID = "Tiền đầu ca không được thấp hơn mức tối thiểu.";
            public const string CASH_AMOUNT_INVALID = "Số tiền không được thấp hơn mức tối thiểu.";
            public const string SHIFT_NOT_OPEN_YET = "Hiện chưa có ca làm đang mở. Vui lòng thử lại sau 1 phút.";
            public const string CASHIER_SHIFT_NOT_OPEN = "Hiện tại chưa có ca làm của Thu ngân. Vui lòng yêu cầu Thu ngân check-in trước.";
            public const string SHIFT_REPORT_NOT_FOUND = "Không tìm thấy báo cáo ca làm này.";
            public const string STAFF_MUST_CHECKOUT_FIRST = "Vui lòng yêu cầu tất cả nhân viên checkout trước khi kết thúc ca của Cashier.";
            public const string UNAUTHORIZED_ACCESS = "Bạn không có quyền thực hiện hành động này.";
        }

        public class ShiftSuccess
        {
            public const string SHIFT_CHECKIN_SUCCESS = "Check-in ca làm thành công.";
            public const string SHIFT_CHECKOUT_SUCCESS = "Check-out ca làm thành công.";
        }
    }
}
