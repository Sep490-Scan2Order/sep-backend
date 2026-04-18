using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Message
{
    public partial class DishMessage
    {
        public class DishError
        {
            public const string DISH_ALREADY_EXISTS = "Tên món ăn đã tồn tại, chọn tên món ăn khác.";
            public const string DISH_NOT_FOUND = "Không tìm thấy món ăn.";
            public const string DISH_OUT_OF_LIMIT = "Số lượng món ăn đã đạt giới hạn, vui lòng nâng cấp để sử dụng thêm.";
            public const string INVALID_DISH_AVAILABILITY = "Số lượng món ăn không được bé hơn số lượng hiện tại.";
            public const string DISH_IMPORT_FILE_INVALID = "File import món ăn không hợp lệ.";
            public const string DISH_COMBO_NOT_FOUND = "Không tìm thấy combo.";
            public const string IMAGE_UPLOAD_ERROR = "Lỗi khi tải ảnh lên: {0}";
            public const string COMBO_MUST_HAVE_AT_LEAST_ONE_DISH = "Combo phải có ít nhất 1 món ăn.";
            public const string ONE_OR_MORE_DISHES_NOT_FOUND = "Một hoặc nhiều món ăn không tồn tại.";
            public const string COMBO_JUST_HAVE_SINGLE_DISH = "Combo chỉ được chứa một món ăn.";
            public const string NO_CATEGORY = "Không có danh mục nào để đồng bộ.";
            public const string NO_DISH = "Không có món ăn nào để đồng bộ.";
            public const string NO_RESTAURANT = "Không có nhà hàng (chi nhánh) nào để đồng bộ.";
        }

        public class DishSuccess
        {
            public const string DISH_CREATED = "Tạo món ăn thành công.";
            public const string DISH_UPDATED = "Cập nhật món ăn thành công.";
            public const string DISH_RETRIEVED = "Lấy món ăn thành công.";
            public const string DISH_AVAILABILITY_UPDATED = "Cập nhật số lượng món ăn thành công.";
            public const string DISH_DELETED = "Xóa món ăn thành công.";
            public const string DISH_DEACTIVE = "Hủy món ăn thành công";
            public const string DISH_ACTIVATED = "Kích hoạt món ăn thành công.";
            public const string DISH_ALREADY_SYNCED = "Tất cả các món ăn đã được đồng bộ trước đó, không cần thêm mới.";
            public const string DISH_SYNC_SUCCESS = "Đã đồng bộ thành công {0} món ăn mới cho các chi nhánh.";
        }


    }
}
