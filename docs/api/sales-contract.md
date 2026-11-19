# Hợp đồng API Sales (đơn, đợt gọi món) — DỰ THẢO

**Trạng thái: dự thảo cho HIE-3.1.** Module Sales chưa có endpoint nào (`/api/sales` còn rỗng) nên mục `Sales` **chưa được thêm vào
[openapi.json](openapi.json)**. File đó được xuất từ host đang chạy (mục 4 của [identity-contract.md](identity-contract.md));
viết tay các đường dẫn chưa tồn tại sẽ làm tài liệu lệch khỏi mã, và CI của `web/` sinh kiểu TypeScript từ chính file này.
Khi endpoint được viết, xuất lại `openapi.json`, chạy `npm run gen:api` trong `web/`, rồi đối chiếu file này với kết quả.

Phần đã chốt trong mã: dịch vụ [OrderService](../../src/Modules/Sales/Restaurant.Modules.Sales.Application/OrderService.cs), kiểu dữ liệu
[OrderModels](../../src/Modules/Sales/Restaurant.Modules.Sales.Application/OrderModels.cs) và mã lỗi `SalesErrors`.
Mô tả hành vi: [sales-order-service.md](../sales-order-service.md). Phần đường dẫn và ánh xạ HTTP bên dưới là **đề xuất**,
theo cùng quy ước với [seating-contract.md](seating-contract.md), cần Hải và Phương duyệt trước khi viết endpoint.

## 1. Endpoint đề xuất (phạm vi đã có dịch vụ)

| Phương thức và đường dẫn | Quyền | `Idempotency-Key` | Thành công |
|---|---|---|---|
| `POST /api/sales/orders` | `sales.orders.manage` | không (xem mục 2) | 201 `OrderDto` |
| `POST /api/sales/orders/{orderId}/batches` | `sales.orders.manage` | **bắt buộc** (là `RequestKey` của dịch vụ) | 201 `BatchDto` (200 khi phát lại) |
| `POST /api/sales/orders/{orderId}/batches/{number}/cancel` | `sales.orders.manage` | không | 200 `BatchDto` |

`sales.orders.manage` được cấp sẵn cho `Manager` và `Staff`. Xác nhận, từ chối đợt (`sales.batches.confirm`), hủy phần món, đóng đơn,
đọc đơn và đợt chờ chưa có dịch vụ nên chưa có trong dự thảo này. Gọi món của khách (QR) theo [ADR-02](../adr/ADR-02-qr-access.md)
dùng cơ chế quyền khác và cũng chưa được mô tả ở đây.

## 2. Dữ liệu

`POST /orders`: `{ type, sessionId, businessDayId, customerName }`.

- `type`: `TAI_BAN` hoặc `MANG_DI`.
- Tại bàn: bắt buộc `sessionId`, **không được** gửi `businessDayId` (ngày bán và nhãn bàn lấy từ phiên).
- Mang về: không có `sessionId`, bắt buộc `businessDayId`. Chưa có contract "ngày đang mở hiện tại" nên client phải gửi ngày.
  Tên, số điện thoại, giờ lấy theo kế hoạch: mới có `customerName`; điện thoại và giờ lấy cần thêm cột trước.
- Người thao tác (`Actor`) lấy từ token, không nhận từ client.
- Một phiên chỉ có một đơn đang mở. Tạo thứ hai ⇒ `409` `sales.order.session_has_open_order`, kể cả khi hai yêu cầu chạy đua.

`POST /orders/{orderId}/batches`: `{ lines: [{ menuItemId, variantId, optionIds, note }] }`.

- Mỗi dòng là một phần món; hai đĩa cùng món là hai dòng. `optionIds` có thể rỗng. Mọi id là `Guid` của Catalog.
- Nguồn đợt (`NHAN_VIEN`) và mã nhân viên lấy từ token. Giá, tên món, trạm chế biến lấy từ Catalog tại thời điểm thêm đợt,
  **không nhận giá từ client**. Giá đã lưu là bản chụp.
- `Idempotency-Key` được dùng làm `RequestKey`. Cùng khóa trả lại đợt cũ (`200`), không tính giá lại, kể cả khi đơn đã đóng.
- Một đợt phải có ít nhất một dòng.

Phản hồi `OrderDto`: `id, code, type, businessDayId, sessionId, tableLabel, customerName, status` (`MO`, `DONG`, `HUY`).
Phản hồi `BatchDto`: `orderId, batchId, number, status, portions[], total`. Mỗi phần món có `unitPrice` = giá biến thể cộng phụ thu từng tùy chọn.
`total` không tính phần đã hủy. Đơn vị tiền là số nguyên VND.

Trạng thái đợt: `CHO_XAC_NHAN`, `DA_XAC_NHAN`, `DA_TU_CHOI`, `DA_HUY`. Phần món: `CHO_XAC_NHAN`, `DA_XAC_NHAN`, `DA_HUY`.
Hủy đợt chỉ áp dụng cho đợt `CHO_XAC_NHAN`; đợt đã xác nhận giảm từng phần món.

## 3. Lỗi

ProblemDetails kèm `code` ổn định; frontend so sánh theo `code`. Ánh xạ đề xuất: `400` cho `*.input.*`, `*.empty`, `*.session_required`,
`*.session_forbidden`, `sales.batch.guest_needs_session`; `404` cho mã kết thúc `.not_found`; còn lại `409`, riêng
`sales.batch.line_invalid` trả `409` kèm danh sách theo dòng.

| `code` | HTTP | Khi nào |
|---|---|---|
| `sales.input.invalid` | 400 | Dữ liệu sai: loại đơn lạ, thiếu hoặc thừa ngày bán, khóa rỗng, dòng rỗng. |
| `sales.order.session_required` | 400 | Đơn tại bàn không có phiên. |
| `sales.order.session_forbidden` | 400 | Đơn mang về có phiên. |
| `sales.batch.empty` | 400 | Đợt không có dòng nào. |
| `sales.order.not_found`, `sales.batch.not_found`, `sales.portion.not_found` | 404 | Không có đối tượng. |
| `sales.order.not_open` | 409 | Đơn đã đóng hoặc hủy. |
| `sales.order.session_has_open_order` | 409 | Phiên đã có đơn mở. |
| `sales.order.changed` | 409 | Đơn bị người khác đổi cùng lúc; tải lại rồi thử lại. |
| `sales.batch.not_pending` | 409 | Hủy đợt không còn chờ xác nhận. |
| `sales.batch.line_invalid` | 409 | Catalog từ chối một số dòng (hết món, tùy chọn sai...); `lineErrors[]` gồm `lineNo` và mã lỗi Catalog. Không thêm gì. |
| `sales.pricing.inconsistent` | 409 | Catalog trả kết quả không khớp số dòng (lỗi hệ thống, không phải lỗi người dùng). |
| `salesday.day.not_found`, `salesday.day.not_open` | 404, 409 | Ngày bán không có hoặc không đang bán. Lấy từ SalesDay. |
| mã phiên của Seating (ví dụ `seating.session.not_found`, `seating.session.closed`) | 404, 409 | Phiên không dùng được. Lấy từ Seating. |

Mã còn lại trong `SalesErrors` (`order.has_pending_batches`, `order.has_active_portions`, `batch.guest_needs_session`,
`portion.already_cancelled`) thuộc các thao tác chưa có dịch vụ (đóng đơn, hủy phần món, đợt của khách).

## 4. Điểm cần quyết định trước khi viết endpoint

1. Đường dẫn và ánh xạ HTTP ở trên (Hải và Phương duyệt).
2. Khách (QR) và nhân viên có dùng chung endpoint hay tách; cách kiểm tra `GuestGrantId` chưa được cài.
3. Cột điện thoại và giờ lấy cho đơn mang về, và contract "ngày bán đang mở".
4. Ghi lại người hủy đợt (domain `CancelBatch` hiện không nhận người thực hiện).
