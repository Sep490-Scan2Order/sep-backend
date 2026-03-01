using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Message
{
    public partial class CategoryMessage
    {
        public class CategoryError
        {
            public const string CATEGORY_ALREADY_EXISTS = "Tên danh mục đã tồn tại, chọn tên danh mục khác.";
            public const string CATEGORY_NOT_FOUND = "Không tìm thấy danh mục.";
            public const string CATEGORY_OUT_OF_LIMIT = "Số lượng danh mục đã đạt giới hạn, vui lòng nâng cấp để sử dụng thêm.";
        }

        public class CategorySuccess
        {
            public const string CATEGORY_CREATED = "Tạo danh mục thành công.";
            public const string CATEGORY_UPDATED = "Cập nhật danh mục thành công.";
            public const string CATEGORY_RETRIEVED = "Lấy danh mục thành công.";
        }
    }
}
