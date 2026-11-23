# Tuần 4 của Phương

## PHU-4.1 · Kho và phiếu nhập

Trong nhóm `/api/inventory`, người có quyền `inventory.catalog.manage` quản lý đơn vị, nguyên liệu, quy đổi riêng theo nguyên liệu và nhà cung cấp. Hệ số từ đơn vị chuẩn về chính nó luôn bằng 1. Mã lỗi `inventory.ingredient.has_stock_or_movements` báo nguyên liệu còn tồn hoặc có phát sinh nên chưa thể lưu trữ, đổi chế độ hay đổi đơn vị chuẩn.

Người có quyền `inventory.purchases.manage` tạo phiếu nháp tại `POST /api/inventory/purchases` với header `Idempotency-Key` là UUID. Gửi lại cùng nội dung, người lập và khóa sẽ nhận lại ID phiếu trong ngày đang bán. Mỗi dòng lưu hệ số, đơn vị chuẩn, chế độ, lượng chuẩn và thành tiền tại thời điểm lập. Cờ `cashDrawer` cho biết khoản mua dùng tiền két. Từ phiếu nháp, gọi `POST /api/inventory/purchases/{id}/post` để ghi hiệu lực hoặc `/cancel` để hủy, với body chứa `dayId`. Chỉnh phiếu đã có hiệu lực thuộc PHU-6.2.

## PHU-4.2 · Bắt đầu và hủy chốt ngày

Các thao tác dưới đây yêu cầu quyền `salesday.manage`.

| Phương thức | Đường dẫn | Kết quả |
| --- | --- | --- |
| GET | `/api/salesday/{dayId}/close-checklist` | Trạng thái ngày và mảng `blockers` gồm mã và số lượng |
| POST | `/api/salesday/{dayId}/begin-close` | Kiểm tra lại dưới khóa độc quyền rồi chuyển từ `DANG_BAN` sang `CHO_CHOT` |
| POST | `/api/salesday/{dayId}/cancel-close` | Trả ngày về `DANG_BAN` để tiếp tục ghi hoặc chỉnh số liệu |

Hai lệnh POST yêu cầu header `Idempotency-Key` là UUID. Nếu còn điều kiện chặn, lệnh bắt đầu chốt trả HTTP 409 với `code = salesday.close.blocked` và `checklist.blockers`. Mã ổn định gồm `salesday.open_orders`, `salesday.open_sessions`, `salesday.pending_batches`, `salesday.pending_fulfillment`, `salesday.pending_adjustments`, `salesday.pending_events`, `salesday.pending_transfers`, `salesday.pending_refunds`, `salesday.draft_purchases` và `salesday.unapproved_counts`.

Lệnh chuyển trạng thái thành công ghi người thao tác, thời điểm và khóa yêu cầu trong cùng giao dịch với ngày bán. Gửi lại khóa đã thành công trả kết quả lần đầu. Ghi dữ liệu nghiệp vụ theo ngày phải qua khóa dùng chung và kiểm tra ngày còn `DANG_BAN`. Kiểm kê, tính giá vốn và chốt cuối cùng sang `DA_CHOT` thuộc tuần 5.

## PHU-4.3 · Gọi thêm và đóng đơn

`InvoiceView.paymentStatus` lấy từ số dư hiện tại. Ví dụ, khách đã trả đủ 100.000đ, sau đó xác nhận thêm món 40.000đ trên cùng đơn. Hóa đơn chuyển từ `DA_THU_DU` sang `THU_MOT_PHAN`, với `netCollected = 100000` và `due = 40000`. Chứng từ đã phát hành giữ số tiền tại thời điểm phát hành.

`POST /api/billing/orders/{orderId}/close` nhận `businessDayId` và yêu cầu quyền `sales.orders.manage`. Hệ thống kiểm tra đợt chờ xác nhận, trạng thái cuối của từng phần món, dòng tính tiền, chênh lệch thu, chuyển khoản và hoàn tiền đang xử lý. Đơn và hóa đơn được đóng trong cùng giao dịch. Phiên bàn có thao tác đóng riêng.

Phần đối chiếu bếp dùng hai bảng `fulfillment.PhieuCheBien` và `fulfillment.SuCoMon` của migration `InitialFulfillment`. Khi triển khai với nhánh bếp của Hiếu, cần kiểm tra một đơn thực qua đủ các bước xác nhận, giao món, thu tiền và đóng. Nếu nhánh bếp chưa được triển khai, phần món đã xác nhận tiếp tục xuất hiện trong danh sách chờ xử lý.

## Migration và kiểm thử

Áp dụng `InitialInventory` cho `InventoryDbContext` và `SalesDayClosingHistory` cho `SalesDayDbContext` trên cùng database với các module Sales, Seating và Billing. Hợp đồng HTTP nằm trong `docs/api/openapi.json`. Kiểu TypeScript tương ứng nằm trong `web/src/lib/api/generated/schema.d.ts`.

Bộ test SQL tạo một database riêng cho từng ca. Ca đóng đơn mô phỏng hai bảng mà nhánh bếp cung cấp để kiểm tra giao dịch đóng đơn trên SQL Server. Chạy các lệnh sau từ thư mục gốc của repo với `IDENTITY_TEST_SQLSERVER` trỏ đến máy chủ SQL thử nghiệm.

```powershell
dotnet test tests/Restaurant.IntegrationTests -c Release --filter FullyQualifiedName~PhuongWeekFourSqlServerTests
dotnet test tests/Restaurant.IntegrationTests -c Release --filter FullyQualifiedName~WeekFourContractTests
```

Ngày 21/11/2026, cả 4 ca SQL tuần 4 đã đạt khi chạy riêng trên SQL Server trong Docker. Khi chạy toàn bộ backend với SQL Server, 5/5 test kiến trúc và 290/290 test tích hợp đạt, 0 ca bị bỏ qua.
