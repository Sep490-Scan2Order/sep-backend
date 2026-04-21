# Kế hoạch thực hiện: Tối ưu luồng Shift và Đối soát chuyển khoản tự động

## Tổng quan
Loại bỏ việc nhập tiền mặt thủ công khi Check-in/Check-out của Cashier. Hệ thống tự động tính toán doanh thu và đối soát với các giao dịch chuyển khoản từ Cashier sang Tenant thông qua các Job chạy định kỳ và theo sự kiện.

## Các thay đổi đề xuất

### 1. Domain Layer
#### [NEW] [ShiftTransfer.cs](file:///d:/Achire/FPTDocument/SEP490/sep-backend/ScanToOrder.Domain/Entities/Shifts/ShiftTransfer.cs)
Thực thể lưu thông tin Cashier chuyển khoản cho Tenant.
- `ShiftId`: Liên kết ca làm việc.
- `Amount`: Số tiền đã chuyển khoản.
- `Note`: Ghi chú.

#### [MODIFY] [ShiftReport.cs](file:///d:/Achire/FPTDocument/SEP490/sep-backend/ScanToOrder.Domain/Entities/Shifts/ShiftReport.cs)
Tối giản hóa các trường dữ liệu:
- **Xóa**: `ExpectedCashAmount`.
- **Logic mới**: `ActualCashAmount` sẽ lưu tổng số tiền đã chuyển khoản (được cập nhật qua Job đối soát).
- **Thêm**: `IsTransferred` (Trạng thái tích xác nhận đã chuyển tiền thành công).

### 2. Application Layer & Background Jobs
- **RecordTransferAsync**: API cho Cashier khai báo chuyển tiền.
- **[NEW] ShiftAutoReconcileJob** (Chạy mỗi 5-10 phút): 
    - Quét các ca chưa hoàn thành đối soát (`IsTransferred = false`).
    - Tính tổng `ShiftTransfer.Amount` và so sánh với `TotalCashOrder`.
    - Nếu khớp -> Cập nhật `ActualCashAmount` và set `IsTransferred = true`.
- **ShiftReminderJob** (Chạy lúc 00:01): Gửi thông báo cho Cashier nếu ca ngày hôm trước vẫn chưa hoàn thành `IsTransferred`.
- **ShiftAlertJob** (Chạy lúc 08:00 sáng): Báo cáo danh sách các ca sai lệch cuối cùng cho Tenant.

### 3. Infrastructure Layer
- Cập nhật `AppDbContext` và tạo Migration.
- Đăng ký các Job vào `HangfireBackgroundJobService`.

## Kế hoạch xác minh

### Kiểm tra tự động
- Viết Unit Test cho `ShiftAutoReconcileJob` để đảm bảo logic so sánh và cập nhật trạng thái `IsTransferred` hoạt động chính xác.

### Kiểm tra thủ công
1. Thực hiện Check-out một ca.
2. Nhập lệnh chuyển khoản trùng khớp với doanh thu.
3. Chờ Job chạy định kỳ (hoặc kích hoạt thủ công) và kiểm tra trạng thái `IsTransferred` có tự động chuyển sang `true` hay không.
