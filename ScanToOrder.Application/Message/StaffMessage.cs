using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Message
{
    public partial  class StaffMessage
    {
        public class StaffError
        {
            public const string STAFF_NOT_FOUND = "Nhân viên không tồn tại.";
            public const string STAFF_ALREADY_EXISTS = "Tài khoản nhân viên đã tồn tại.";
            public const string STAFF_CREATION_FAILED = "Tạo nhân viên thất bại.";
            public const string STAFF_UPDATE_FAILED = "Cập nhật thông tin nhân viên thất bại.";
            public const string STAFF_DELETE_FAILED = "Xóa nhân viên thất bại.";
            public const string INVALID_RESTAURANT = "Nhà hàng được chọn không tồn tại hoặc không hợp lệ.";
            public const string UNAUTHORIZED_ACCESS = "Bạn không có quyền thực hiện thao tác này cho nhân viên này.";
        }

        public class StaffSuccess
        {
            public const string STAFF_CREATED = "Tạo tài khoản nhân viên thành công.";
            public const string STAFF_UPDATED = "Cập nhật thông tin nhân viên thành công.";
            public const string STAFF_DELETED = "Xóa nhân viên thành công.";
        }
    }
}
