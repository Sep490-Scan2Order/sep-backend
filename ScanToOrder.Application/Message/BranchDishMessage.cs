using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Message
{
    public partial class BranchDishMessage
    {
        public class BranchDishError
        {
            public const string BRANCH_DISH_NOT_FOUND = "Không tìm thấy món ăn tại chi nhánh.";
            public const string BRANCH_DISH_ALREADY_EXISTS = "Món ăn đã tồn tại tại chi nhánh.";
            public const string INVALID_SOLD_OUT_STATUS = "Trạng thái bán không hợp lệ.";
            public const string BRANCH_DISH_NOT_BELONG_TO_RESTAURANT = "Món ăn không thuộc nhà hàng này.";
        }

        public class BranchDishSuccess
        {
            public const string BRANCH_DISH_RETRIEVED = "Lấy danh sách món ăn chi nhánh thành công.";
            public const string BRANCH_DISH_SOLD_OUT_UPDATED = "Cập nhật trạng thái bán thành công.";
            public const string BRANCH_DISH_CREATED = "Thêm món ăn vào chi nhánh thành công.";
            public const string BRANCH_DISH_REMOVED = "Xóa món ăn khỏi chi nhánh thành công.";
        }
    }
}
