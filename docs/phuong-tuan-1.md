# Bàn giao phần việc Phương tuần 1

Phạm vi: PHU-1.1, PHU-1.2 và PHU-1.3, dựa trên kế hoạch 7 tuần và thiết kế dữ liệu hiện tại trong repository.

## SalesDay

GET `/api/salesday/current`: yêu cầu đăng nhập; trả ngày đang hoạt động hoặc null.
POST `/api/salesday/open`: yêu cầu quyền `salesday.manage`, header `Idempotency-Key` là UUID khác rỗng.

```json
{"businessDate":"2026-11-09","openingCash":1000000}
```

Trả 201 khi tạo, 200 khi phát lại cùng khóa và nội dung (kể cả người mở). DTO gồm id, businessDate, openingCash, status, openedAt. Tiền là số nguyên VND; thời gian UTC. Lỗi ProblemDetails có code: salesday.idempotency.required (400), salesday.input.invalid (400), salesday.idempotency.conflict (409), salesday.previous_not_closed (409), salesday.date.invalid (409). Chưa đăng nhập: 401; thiếu quyền: 403.

Khóa ứng dụng toàn cục trong transaction tuần tự hóa mở ngày. Unique filtered index ActiveSlot bảo vệ cả khi ghi trực tiếp ngoài service; trạng thái CHO_CHOT vẫn chiếm vị trí ngày hoạt động. Ngày đã chốt không được mở lại; ngày mới phải sau ngày trước.

## Catalog

9 bảng: NhomMon, Mon, BienThe, NhomTuyChon, TuyChon, MonNhomTuyChon, HetMon, HetBienThe, HetTuyChon. Có khóa ngoại nội module, giá không âm, mã món duy nhất, mã biến thể duy nhất trong món, giới hạn số tùy chọn và rowversion. NgayBanId và người cập nhật là tham chiếu liên module, không tạo FK chéo schema. API quản lý menu thuộc tuần sau.

## Kiểm chứng và tích hợp

Chạy từ thư mục repository:

```powershell
dotnet restore RestaurantManagement.slnx --locked-mode
dotnet build RestaurantManagement.slnx -c Release --no-restore
dotnet test RestaurantManagement.slnx -c Release --no-build --no-restore
dotnet format whitespace RestaurantManagement.slnx --verify-no-changes --no-restore
```

Test SQL sử dụng IDENTITY_TEST_SQLSERVER giống hạ tầng test hiện tại. Chuỗi kết nối phải trỏ đến máy SQL Server thử nghiệm có quyền tạo database. Mỗi test tạo database riêng có tiền tố RestaurantPhuongTest_; chỉ database tự tạo được xóa. Nếu chưa cấu hình biến này, test SQL bị skip, không được coi là đạt nghiệm thu SQL.

Chưa triển khai staging, commit hoặc push. Chủ module cần review và nhóm thực hiện nghiệm thu staging theo kế hoạch.

## Migration

Đặt ConnectionStrings__Restaurant vào môi trường của terminal (không ghi mật khẩu vào Git), rồi sinh script để người triển khai xem và áp dụng:

```powershell
dotnet tool restore
dotnet ef migrations script --idempotent --project src/Modules/SalesDay/Restaurant.Modules.SalesDay.Infrastructure --context SalesDayDbContext --output salesday.sql
dotnet ef migrations script --idempotent --project src/Modules/Catalog/Restaurant.Modules.Catalog.Infrastructure --context CatalogDbContext --output catalog.sql
```

Mỗi module có schema và bảng lịch sử migration riêng theo factory hiện hữu. API không tự chạy migration khi khởi động.

## CI SQL

Workflow hiện tại chỉ chọn FullyQualifiedName~IdentitySqlServer. Hải cần mở rộng bộ lọc thành `FullyQualifiedName~IdentitySqlServer|FullyQualifiedName~PhuongWeekOneSqlServerTests` trong bước SQL, hoặc bỏ bộ lọc khi biến IDENTITY_TEST_SQLSERVER đã được cấu hình. Chưa sửa workflow chung trong bản bàn giao này.

## Kết quả kiểm tra cục bộ ngày 10 tháng 11 năm 2026

- Restore locked-mode: đạt sau khi đặt NuGet cache trong workspace.
- Build Release: đạt, 0 warning, 0 error.
- Format whitespace theo folder cho các file thay đổi: đạt. Chế độ solution không chạy được vì BuildHost named pipe bị từ chối truy cập.
- git diff --check: đạt.
- EF has-pending-model-changes: cả SalesDay và Catalog đều không có thay đổi chưa nằm trong migration.
- Đã sinh script idempotent: docs/salesday-initial.sql và docs/catalog-initial.sql.
- Đã chạy scripts/Test-PhuongWeek1.ps1 từ PowerShell của người dùng với SQL Server 2022 trong Docker: 5/5 test đạt, 0 lỗi, 0 bỏ qua. Đối chiếu file TestResults/PhuongWeek1/phuong-week1.trx: passed=5, failed=0, notExecuted=0. Các test bao gồm C03, mở ngày đồng thời/chống lặp, ràng buộc ngày hoạt động và ràng buộc Catalog. Chưa chạy toàn bộ test của repository, CI hoặc nghiệm thu staging.
