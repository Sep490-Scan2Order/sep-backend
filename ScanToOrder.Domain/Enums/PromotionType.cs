using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Enums
{
    public enum PromotionType
    {
        Standard,       // 0: Khuyến mãi thường (Chạy theo ngày StartDate -> EndDate)
        HappyHour,      // 1: Giờ vàng - Bắt buộc nhập khung giờ
        Clearance,      // 2: Xả hàng - Thường giảm sâu, ưu tiên đẩy hàng tồn
        WeeklySpecial   // 3: Ngày trong tuần - Bắt buộc chọn thứ (2, 4, 6...)
    }
}
