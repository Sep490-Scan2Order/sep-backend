# 🍽️ ScanToOrder - Backend APIs (SEP490)

Chào mừng bạn đến với kho lưu trữ mã nguồn Backend của dự án **ScanToOrder** - Nền tảng SaaS quản lý F&B (Nhà hàng, Quán ăn) thông qua hệ thống quét mã QR gọi món, đa người thuê (Multi-tenant).

Dự án được triển khai dựa trên nền tảng **.NET 8** và áp dụng các tiêu chuẩn thiết kế phần mềm linh hoạt (Clean Architecture), cho phép khả năng mở rộng tối đa và dễ dàng kiểm thử.

---

## 🏗️ Kiến Trúc Hệ Thống (Clean Architecture)
Dự án được chia làm 4 dự án (Projects) độc lập, xoay quanh phần lõi nghiệp vụ (Domain-Centric) để đảm bảo tuân thủ nguyên lý đảo ngược phụ thuộc (Dependency Inversion) của SOLID:

1. **`ScanToOrder.Domain` (Tầng Lõi):**
   - Chứa Cấu trúc dữ liệu (Entities), Các Enum, hằng số (Constants/Messages), và Exceptions dành riêng cho nghiệp vụ (Domain Exceptions).
   - Tầng này **KHÔNG** tham chiếu đến bất kỳ tầng nào khác. Được viết bằng C# thuần.
   
2. **`ScanToOrder.Application` (Tầng Ứng Dung):**
   - Chứa các quy tắc nghiệp vụ hệ thống (Business Logic/Services).
   - Định nghĩa DTOs, Interfaces (như `IGenericRepository`, `IUnitOfWork`, `IRedisService`).
   - Tầng này chỉ được phép tham chiếu tới `ScanToOrder.Domain`.

3. **`ScanToOrder.Infrastructure` (Tầng Hạ Tầng):**
   - Chứa mã kết nối với bên thứ ba: Triển khai DbContext (EF Core / PostgreSQL), Repositories theo Interface từ Application, mã giao tiếp Redis/SendGrid/VNPAY/AWS S3, v.v.
   - Trực tiếp tương tác với công nghệ phần cứng/mạng. Tham chiếu `Domain` & `Application`.

4. **`ScanToOrder.Api` (Tầng Trình Diễn):**
   - Giao thức đầu vào cuối cùng (Controllers, Program.cs, Middleware, Filter).
   - Chịu trách nhiệm Cấu hình Dependency Injection (DI), xác thực token JWT, Swagger.
   - Chỉ chứa các lệnh điều hướng luồng đến `Application`. Tham chiếu tới `Application` và `Infrastructure`.

---

## 🛠️ Công Nghệ & Công Cụ (Tech Stack)
- **Framework:** .NET 8 (C# 12)
- **Cơ sở dữ liệu:** PostgreSQL (Kết hợp thư viện Pgvector để search AI).
- **ORM:** Entity Framework Core (Code-First Migration).
- **Caching & Session:** Redis (Sử dụng luồng đa tuyến cho tính năng Cart & Transaction).
- **Realtime / Socket:** SignalR (Đồng bộ nhà bếp & đơn hàng).
- **Testing:** xUnit, Moq, FluentAssertions, Coverlet.

---

## 🧪 Hệ thống Kiểm Thử & Chạy Test (Unit Test)
Việc kiểm tra chất lượng mã nguồn (Code Coverage) là cực kỳ được chú trọng ở dự án này. Hệ thống được bao phủ bởi các Test Projects tương đương cho các tầng:

- `ScanToOrder.Application.UnitTest`: Mock Service và test mọi kịch bản ở tầng logic.
- `ScanToOrder.Infrastructure.UnitTest`: *(Đang xây dựng)*.
- `ScanToOrder.Domain.UnitTest`: *(Đang thiết lập)*.

### 🚀 Hướng Dẫn Chạy Test Tự Động (Automation Script)
Bên team đã xây dựng sẵn file thực thi lệnh `.bat` giúp người kiểm thử (hoặc Developer) dọn dẹp bộ nhớ đệm, tự động chạy tất cả các bài Test và trực tiếp sinh ra Website báo cáo kết quả:

1. Điều hướng Terminal (hoặc mở Explorer) tại ngay thư mục gốc của project (có chứa file `ScanToOrder_BE_SEP490.sln`).
2. Gõ lệnh trên Terminal: `.\run-tests.bat` (Hoặc bấm đúp chuột vào file **`run-tests.bat`** trên máy tính).
3. Đợi tiến trình hiển thị PASSED. Một thư mục `CodeCoverageReport` sẽ xuất hiện ngay đó.
4. Mở file **`CodeCoverageReport/index.html`** trên bất kì trình duyệt (Chrome/Edge) nào để tận hưởng bảng phân tích màu sắc trực quan (Đỏ/Xanh/Vàng).

---

## 📦 Hướng Dẫn Cài Đặt Ban Đầu
*(Tuỳ chỉnh thêm các lệnh sau vào nếu máy Dev mới kéo code về)*

1. Cài đặt các IDE như Visual Studio 2022 hoặc Jetbrains Rider.
2. Phục hồi thư viện:
   ```bash
   dotnet restore ScanToOrder_BE_SEP490.sln
   ```
3. Khởi tạo Database (Migration):
   > Lưu ý: Hãy chắc chắn chuỗi kết nối (Connection String) trong `appsettings.json` trỏ đúng vào CSDL PostgreSQL máy bạn trước khi chạy lệnh.
   ```bash
   cd ScanToOrder.Infrastructure
   dotnet ef database update -s ../ScanToOrder.Api/ScanToOrder.Api.csproj
   ```
4. Khởi chạy dự án API:
   ```bash
   cd ScanToOrder.Api
   dotnet run
   ```
   > Dashboard API Swagger sẽ hiện lên ở cổng (Ví dụ: `https://localhost:7xxx/swagger`).

---
*(Tài liệu này là một phiên bản mô tả kiến trúc nhanh do Bot AI biên tập - SEP490 Team)*
