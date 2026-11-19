# Sales: tạo đơn, thêm đợt, hủy đợt (HIE-3.1, phần đầu)

Phần này viết `OrderService` ở lớp Application của Sales theo đúng các contract đã chốt, và chạy bằng đối tượng giả.
Chưa có endpoint, chưa có lớp lưu trữ EF thật.

## Đã có

| Thành phần | Vị trí |
| --- | --- |
| `OrderService`: `CreateOrderAsync`, `AddBatchAsync`, `CancelBatchAsync` | `Sales.Application/OrderService.cs` |
| Cổng lưu trữ `IOrderStore` / `IOrderTransaction` (một transaction, hủy nếu không `CommitAsync`) | `Sales.Application/OrderPorts.cs` |
| Lệnh, kết quả, view | `Sales.Application/OrderModels.cs` |
| Đối tượng giả: `FakeMenuPricing`, `FakeSalesDayGate`, `FakeInvoiceCharging`, `FakeSessionLookup`, `InMemoryOrderStore` | `tests/Restaurant.IntegrationTests/SalesFakes/` |
| Migration `SalesCatalogGuidIds` | `Sales.Infrastructure/Persistence/Migrations/` |

## Quy tắc đã cài

- **Thứ tự khóa (ADR-01)**: mở transaction, qua `ISalesDayGate` (khóa ngày), rồi mới khóa đơn. Test kiểm tra thứ tự lời gọi.
  Với đơn đã có, ngày được đọc không khóa (`FindBusinessDayAsync`, ngày của đơn không đổi), qua cổng, rồi mới `LockOrderAsync`.
- **Đơn tại bàn**: bắt buộc có phiên. Ngày bán và nhãn bàn lấy từ `ISessionLookup` (không nhận ngày từ client để tránh mâu thuẫn).
  Một phiên chỉ có một đơn mở; trùng thì `sales.order.session_has_open_order`, kể cả khi hai yêu cầu chạy đua (lỗi từ chỉ mục duy nhất).
- **Đơn mang về**: không có phiên, client gửi `BusinessDayId`.
- **Thêm đợt**: giá và tình trạng món lấy từ `IMenuPricing` theo ngày của đơn. Giá lưu là bản chụp (`GiaMonChot` = giá biến thể,
  phụ thu từng tuỳ chọn lưu riêng). Lỗi từ menu trả về theo từng dòng (`sales.batch.line_invalid` + `LineErrors`), không thêm gì.
- **Idempotency**: cùng `RequestKey` trả lại đợt cũ (`Replayed = true`), không đánh giá lại giá, kể cả khi đơn đã đóng.
- **Hủy đợt**: chỉ đợt đang chờ xác nhận (luật của `DonHang.CancelBatch`).
- Xung đột ghi (`StoreConflictException`) được đổi thành mã lỗi nghiệp vụ, transaction được hủy.

## Thay đổi lược đồ

Catalog và `IMenuPricing` dùng `Guid` cho món, biến thể, tuỳ chọn, nhóm tuỳ chọn, còn migration Sales đầu tiên lưu `bigint`.
Không có cách nào chép một dòng đã tính giá vào phần món nếu giữ `bigint`, nên Sales đổi bốn cột
(`ChiTietDon.MonId/BienTheId`, `TuyChonDaDat.TuyChonId/NhomTuyChonId`) sang `uniqueidentifier`.
SQL Server không đổi trực tiếp `bigint` sang `uniqueidentifier`, nên migration xóa rồi thêm cột. Migration dừng với lỗi rõ ràng
nếu đã có dòng dữ liệu (hiện chưa thể có vì chưa có endpoint nào tạo đơn). Có test cho cả hướng lên, hướng xuống và trường hợp bị chặn.

## Chưa làm / cần quyết định

- **Lớp lưu trữ thật** (`IOrderStore` bằng `SalesDbContext`: `UPDLOCK` khi khóa đơn, sinh mã `MaDon`, bắt lỗi 2601 và lỗi `rowversion`
  để ném `StoreConflictException`) và đăng ký DI. Chưa đăng ký `OrderService` vì chưa có cài đặt cổng.
- **Endpoint** và phân quyền (cần quyết định quyền cho nhân viên và khách).
- **Xác nhận, từ chối đợt, hủy phần món, kiểm tra giá đổi khi xác nhận**: `FakeInvoiceCharging` đã có sẵn cho bước xác nhận (test 5).
- **Đơn mang về theo kế hoạch có tên, điện thoại, giờ lấy**: `DonHang` hiện chỉ có `TenKhach`. Số điện thoại và giờ lấy cần thêm cột; chưa làm.
- **Ngày bán của đơn mang về**: chưa có contract "ngày đang mở hiện tại" từ SalesDay, nên client phải gửi `BusinessDayId`.
- Người hủy đợt chưa được ghi lại (domain `CancelBatch` không nhận người thực hiện).
- Quyền của khách (`GuestGrantId`) chưa được kiểm tra ở đây; việc đó thuộc phần xác thực khách.
