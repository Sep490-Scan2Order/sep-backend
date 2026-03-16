using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Message
{
    public partial class OrderMessage
    {
        public class OrderError
        {
            public const string QR_INVALID = "QR không hợp lệ.";
            public const string QR_ORDER_ID_INVALID = "OrderId trong QR không hợp lệ.";
            public const string ORDER_NOT_FOUND = "Order không tồn tại.";
            public const string QR_ALREADY_SCANNED = "QR đã được scan trước đó.";
            public const string ORDER_NOT_READY = "Đơn chưa sẵn sàng.";
        }
    }
}
