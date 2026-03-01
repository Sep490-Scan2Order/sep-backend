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
        }

        public class DishSuccess
        {
            public const string DISH_CREATED = "Tạo món ăn thành công.";
            public const string DISH_UPDATED = "Cập nhật món ăn thành công.";
            public const string DISH_RETRIEVED = "Lấy món ăn thành công.";
            public const string DISH_AVAILABILITY_UPDATED = "Cập nhật số lượng món ăn thành công.";
        }
    }
}
