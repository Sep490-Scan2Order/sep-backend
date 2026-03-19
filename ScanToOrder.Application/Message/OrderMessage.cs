
﻿using System;
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
            public const string QUANTITY_MUST_BE_GREATER_THAN_ZERO = "Số lượng phải lớn hơn 0.";
            public const string CANNOT_ADD_DISH_FROM_OTHER_RESTAURANT = "Không thể thêm món của nhà hàng khác vào cùng một giỏ hàng.";
            public const string CART_ID_REQUIRED = "CartId không được để trống.";
            public const string CART_NOT_FOUND_OR_EXPIRED = "Giỏ hàng không tồn tại hoặc đã hết hạn.";
            public const string INVALID_CART_DATA = "Dữ liệu giỏ hàng không hợp lệ.";
            public const string CART_EMPTY_CANNOT_CREATE_PAYMENT = "Giỏ hàng trống, không thể tạo mã thanh toán.";
            public const string RESTAURANT_NO_BANK_CONFIGURED = "Nhà hàng chưa cấu hình tài khoản ngân hàng để nhận thanh toán.";
            public const string RESTAURANT_BANK_NOT_VERIFIED = "Tài khoản ngân hàng của nhà hàng chưa được xác thực.";
            public const string PHONE_REQUIRED = "Số điện thoại không được để trống.";
            public const string DISH_OUT_OF_STOCK = "Món {0} đã hết số lượng.";
            public const string CART_EMPTY_CANNOT_CREATE_ORDER = "Giỏ hàng trống, không thể tạo đơn hàng.";
            public const string INVALID_ORDER_ID = "OrderId không hợp lệ.";
            public const string STAFF_NOT_IDENTIFIED = "Không xác định được nhân viên đăng nhập.";
            public const string CASH_TRANSACTION_NOT_FOUND = "Giao dịch tiền mặt không tồn tại.";
            public const string ORDER_SEQUENCE_NOT_FOUND_IN_RESTAURANT = "Không tìm thấy đơn hàng với số thứ tự này tại nhà hàng của bạn.";
            public const string PAYMENT_CODE_REQUIRED = "PaymentCode không được để trống.";
            public const string INVALID_PAYMENT_AMOUNT = "Số tiền thanh toán không hợp lệ.";
            public const string TRANSACTION_NOT_FOUND = "Giao dịch không tồn tại.";
            public const string ORDER_FROM_PAYMENT_CODE_NOT_FOUND_OR_EXPIRED = "Không tìm thấy đơn hàng từ mã thanh toán hoặc đã hết hạn.";
            public const string INVALID_ORDER_CODE = "Mã đơn hàng không hợp lệ.";
            public const string PAYMENT_AMOUNT_MISMATCH = "Số tiền thanh toán không khớp với tổng tiền đơn hàng.";
            public const string DISH_ID_LIST_REQUIRED = "Danh sách DishId không được để trống.";
            public const string ORDER_ALREADY_CANCELLED_OR_REFUNDED = "Đơn hàng đã được hủy hoặc hoàn tiền trước đó.";
            public const string ONLY_CONFIRM_UNPAID_ORDER = "Chỉ có thể xác nhận thanh toán cho đơn hàng đang ở trạng thái Chưa thanh toán.";
            public const string SYSTEM_ERROR_MANUAL_ONLY = "Lỗi hệ thống trong mô hình trả trước chỉ được xử lý qua chức năng Xác nhận thanh toán thủ công.";
        }

        public class OrderSuccess
        {
        }
    }
}
