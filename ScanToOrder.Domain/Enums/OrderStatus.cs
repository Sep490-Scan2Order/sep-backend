using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Enums
{
    public enum OrderStatus
    {
        Pending = 0,    // Đang chờ (Khách đặt xong, chờ thu ngân/bếp nhận đơn)
        Preparing = 1,  // Đang làm (Bếp đã nhận và đang nấu)
        Ready = 2,      // Đã hoàn thành (Nấu xong, để ở quầy chờ mang ra bàn)
        Served = 3,     // Đã giao (Khách đã nhận được đồ ăn)
        Cancelled = 4   // Đã hủy (Hết món, khách đổi ý...)
    }
}
