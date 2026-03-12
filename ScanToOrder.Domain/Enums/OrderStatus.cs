using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Enums
{
    public enum OrderStatus
    {
        Unpaid = 0,
        Pending = 1,    // Đang chờ (Khách đặt xong, chờ thu ngân/bếp nhận đơn)
        Preparing = 2,  // Đang làm (Bếp đã nhận và đang nấu)
        Ready = 3,      // Đã hoàn thành (Nấu xong, để ở quầy chờ mang ra bàn)
        Served = 4,     // Đã giao (Khách đã nhận được đồ ăn)
        Cancelled = 5   // Đã hủy (Hết món, khách đổi ý...)
    }
}
