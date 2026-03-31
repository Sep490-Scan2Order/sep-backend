# ScanToOrder - Backend APIs (SEP490)

ScanToOrder là nền tảng SaaS chuyên dụng cho hệ thống quản lý F&B (Nhà hàng, Quán ăn, Cafe) tập trung vào tính năng nòng cốt: Quét mã QR tại bàn để gọi món (Scan-to-Order).

Dự án triển khai mô hình đa khách hàng (Multi-tenant), cho phép mỗi nhà hàng (Tenant) hoạt động độc lập trên cùng một hệ thống với dữ liệu, menu, nhân sự và báo cáo doanh thu được cô lập hoàn toàn.

---

## 1. Các tính năng nổi bật

*   **Multi-tenant Architecture:** Quản lý cơ cấu dữ liệu tách biệt an toàn giữa các Nhà Hàng và Chi Nhánh.
*   **Realtime Kitchen Sync:** Đồng bộ tức thời trạng thái đơn hàng (Khách hàng - Thu Ngân - Nhà Bếp) bằng WebSockets (SignalR).
*   **High-Performance Caching:** Ứng dụng Redis xử lý tác vụ giỏ hàng (Cart) và phiên giao dịch (Transaction Session), đảm bảo hiệu năng trong thời gian cao điểm.
*   **Semantic Search:** Tích hợp công nghệ Vector Database (Pgvector) trong PostgreSQL hỗ trợ tìm kiếm ngữ nghĩa cho hệ thống món ăn.
*   **Payment Gateway Integrations:** Tích hợp thanh toán linh hoạt qua VNPAY và chuyển khoản ngân hàng.
*   **Advanced Promotion Engine:** Hệ thống cấu hình khuyến mãi chi tiết (chiết khấu phần trăm, cố định, giảm tối đa, theo khung giờ).

---

## 2. Kiến trúc hệ thống (Clean Architecture)

Dự án áp dụng mô hình Clean Architecture được chia thành 4 phân hệ (Projects) chính để đảm bảo tính độc lập của nghiệp vụ, dễ bảo trì và phục vụ tốt cho quá trình Unit Test.

### 2.1. ScanToOrder.Domain (Tầng Lõi Nghiệp Vụ)
Nằm ở tâm điểm kiến trúc và không phụ thuộc vào bất kỳ thư viện bên thứ 3 nào.
*   `Entities/`: Chứa các thực thể cốt lõi (Cart, Dish, Order, Promotion, Restaurant, Tenant, v.v).
*   `Interfaces/`: Định nghĩa các Abstractions cốt lõi (như `IGenericRepository`, `IUnitOfWork`).
*   `Exceptions/`: Quản lý các ngoại lệ nghiệp vụ (`DomainException`).

### 2.2. ScanToOrder.Application (Tầng Ứng Dụng)
Chứa toàn bộ logic nghiệp vụ (Use-cases). Giao tiếp với hạ tầng thông qua các Interfaces được định nghĩa sẵn.
*   `Services/`: Lớp logic xử lý kịch bản ứng dụng (`OrderService`, `PromotionService`, v.v).
*   `DTOs/`: Data Transfer Objects vận chuyển dữ liệu giữa các tầng.
*   `Mappings/`: Cấu hình thư viện AutoMapper.
*   `Validators/`: Định nghĩa ràng buộc dữ liệu đầu vào (FluentValidation).
*   `Interfaces/`: Xuất bản các Service Interfaces như `ICartRedisService`, `IStorageService`.

### 2.3. ScanToOrder.Infrastructure (Tầng Hạ Tầng)
Chịu trách nhiệm thực thi các tương tác với thế giới bên ngoài (Database, File Storage, Network).
*   `Context/`: Triển khai DbContext của Entity Framework.
*   `Repositories/`: Triển khai cụ thể các Repository khai báo từ tầng Domain (`GenericRepository`).
*   `Migrations/`: Theo dõi các phiên bản thay đổi lược đồ (Schema) của cơ sở dữ liệu.
*   `Hubs/`: Nơi xử lý WebSockets của SignalR.
*   `Services/`: Thực thi các dịch vụ bên ngoài (VNPAY Service, AWS S3 upload, Sendgrid email, Redis Connection).

### 2.4. ScanToOrder.Api (Tầng Trình Diễn)
Điểm tiếp xúc duy nhất với phía Client (Restful APIs).
*   `Controllers/`: Các endpoint API giao tiếp với các Frontend platform.
*   `Program.cs` / `Startup`: Cấu hình Dependency Injection (DI) hệ thống toàn cục.
*   `Middleware/`: Bắt lỗi chung (Global Exception Handling), chứng thực Authentication/Authorization và CORS.

---

## 3. Yêu cầu hệ thống (Prerequisites)

*   **.NET 8.0 SDK**
*   **PostgreSQL** (yêu cầu cài đặt extension `pgvector`). *Khuyến nghị sử dụng Docker Image `ankane/pgvector`.*
*   **Redis Server** (cổng mặc định 6379).
*   IDE khuyên dùng: Visual Studio 2022 hoặc Jetbrains Rider.

---

## 4. Hướng dẫn thiết lập và chạy dự án

### Bước 1: Thiết lập biến môi trường
Bên trong project `ScanToOrder.Api`, tìm và mở file **`appsettings.Development.json`**. Khai báo chính xác thông tin chuỗi kết nối:

```json
"ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=ScanToOrderDb;Username=postgres;Password=your_password",
    "RedisConnection": "localhost:6379"
}
```

### Bước 2: Apply Migrations (Khởi động lược đồ DB)
Mở cửa sổ dòng lệnh tại gốc dự án, chuyển hướng tới project Infrastructure và thực thi lệnh sinh DB:

```bash
cd ScanToOrder.Infrastructure
dotnet ef database update -s ../ScanToOrder.Api/ScanToOrder.Api.csproj
```

### Bước 3: Khởi chạy API
Khởi chạy thông qua giao diện của IDE hoặc dòng lệnh:

```bash
cd ScanToOrder.Api
dotnet run
```
Truy cập danh sách API tài liệu nội bộ tại: `http://localhost:<port>/swagger`

---

## 5. Hướng dẫn kiểm thử (Unit Testing & Coverage)

Dữ án triển khai Unit Test tập trung vào tầng nghiệp vụ (`ScanToOrder.Application.UnitTest`) với các framework chuyên dụng:
*   **xUnit**: Test engine cấu trúc điều phối kịch bản.
*   **Moq**: Giả lập các dependencies (Mocks/Stubs) để cách ly Data layer.
*   **FluentAssertions**: Triển khai thiết kế TDD thông qua hệ thống ngữ cảnh code tự nhiên.

### Cách chạy Test tự động và thu thập báo cáo Code Coverage

Dự án cung cấp tệp thực thi batch **`run-tests.bat`** tại thư mục gốc nhằm khởi chạy tác vụ dò code Coverage.  
Process bao gồm:
1. Dọn dẹp cache `TestResults` dư thừa nếu có.
2. Build giải pháp và thực thi lệnh Coverlet.
3. Chuyển đổi file XML sang định dạng HTML bằng `ReportGenerator`.

**Câu lệnh thực thi:**
*   Mở Terminal tại gốc thư mục, gõ `.\run-tests.bat` (Hoặc chạy file .bat bằng đúp chuột).

**Kiểm tra kết quả:**
*   Sau khi quy trình hoàn tất, truy cập thư mục `CodeCoverageReport` và mở file `index.html` bằng trình duyệt web bất kỳ. Tại đây các dòng Line Coverage & Branch Coverage cho từng Function sẽ được hiển thị chi tiết.
