# Hợp đồng API Seating (khu vực, bàn, phiên bàn)

Nguồn sự thật cho đường dẫn, kiểu dữ liệu và mã trạng thái là [openapi.json](openapi.json) (tag `Seating`). File này giải thích
thêm quyền, quy ước và mã lỗi mà OpenAPI không nói hết. Cách tạo lại `openapi.json` ở mục 4 của
[identity-contract.md](identity-contract.md). Task: HIE-2.1.

## 1. Endpoint và quyền

Mọi endpoint cần `Authorization: Bearer <accessToken>`. Quyền lấy từ claim `permission` của token.

| Phương thức và đường dẫn | Quyền | `Idempotency-Key` | Thành công |
|---|---|---|---|
| `GET /api/seating/areas` | `seating.catalog.manage` | không | 200 danh sách `AreaDto` |
| `POST /api/seating/areas` | `seating.catalog.manage` | không | 201 `AreaDto` |
| `PUT /api/seating/areas/{id}` | `seating.catalog.manage` | không | 200 `AreaDto` |
| `POST /api/seating/areas/{id}/archive` | `seating.catalog.manage` | không | 200 `AreaDto` |
| `GET /api/seating/tables` | `seating.catalog.manage` | không | 200 danh sách `TableDto` |
| `POST /api/seating/tables` | `seating.catalog.manage` | không | 201 `TableDto` |
| `PUT /api/seating/tables/{id}` | `seating.catalog.manage` | không | 200 `TableDto` |
| `POST /api/seating/tables/{id}/archive` | `seating.catalog.manage` | không | 200 `TableDto` |
| `GET /api/seating/board` | `seating.sessions.manage` | không | 200 danh sách `BoardAreaDto` (chỉ khu vực đang dùng) |
| `POST /api/seating/tables/{tableId}/sessions` | `seating.sessions.manage` | **bắt buộc** | 201 `SessionDto` (200 khi phát lại) |
| `POST /api/seating/sessions/{sessionId}/transfer` | `seating.sessions.manage` | **bắt buộc** | 200 `SessionDto` |
| `POST /api/seating/sessions/{sessionId}/close` | `seating.sessions.manage` | **bắt buộc** | 200 `SessionDto` |

Vai trò được cấp sẵn (migration `InitialIdentity`): `Manager` có cả hai quyền; `Staff` chỉ có `seating.sessions.manage`;
`Admin`, `Kitchen`, `Bar` không có quyền nào trong hai quyền này. Vì vậy màn quản lý khu vực và bàn chỉ dành cho `Manager`,
còn bảng trạng thái bàn và mở, chuyển, đóng phiên dùng được với `Staff`. Tài khoản `Admin` gọi các endpoint này nhận `403`.

## 2. Quy ước

- **`Idempotency-Key`**: header là UUID khác rỗng. Thiếu hoặc sai định dạng ⇒ `400` mã `seating.idempotency.required`. Cùng khóa và
  cùng nội dung trả lại kết quả lần đầu (mở phiên trả `200` thay vì `201`); cùng khóa nhưng khác nội dung ⇒ `409`
  `seating.idempotency.conflict`. Khóa có phạm vi theo người thao tác. Frontend sinh khóa mới cho mỗi ý định của người dùng và
  giữ nguyên khi thử lại.
- **`version`**: chuỗi do server cấp cho khu vực, bàn và phiên. Gửi lại **nguyên văn** khi sửa hoặc lưu trữ (`UpdateAreaRequest`,
  `UpdateTableRequest`, `ArchiveRequest`). Bản đã cũ ⇒ `409` `seating.concurrency.conflict`; frontend tải lại rồi cho người dùng thử lại.
- **Lỗi nghiệp vụ**: ProblemDetails kèm trường `code` ổn định; frontend so sánh theo `code`, không theo `title`. Ánh xạ trạng thái:
  `400` cho `seating.input.invalid`, `seating.idempotency.required`, `seating.transfer.same_table`; `404` cho mọi mã kết thúc
  bằng `.not_found`; mọi mã còn lại `409`.
- **Ngày bán**: mở, chuyển, đóng phiên cần có ngày bán đang mở, nếu không ⇒ `409` `salesday.day.not_open`.
- **Lưu trữ, không xóa**: khu vực và bàn chỉ có thể lưu trữ (`active = false`), không có endpoint xóa. Lưu trữ lại đối tượng đã
  lưu trữ trả `200` với trạng thái hiện tại, không báo lỗi.
- **Trạng thái bàn trên bảng**: `status` là `TRONG` hoặc `DANG_DUNG`; `session` chỉ có khi bàn đang dùng.
- `TableDto.publicQrCode` là mã định vị bàn. Theo [ADR-02](../adr/ADR-02-qr-access.md) mã này **một mình không đủ để đặt món**.

## 3. Mã lỗi

| `code` | HTTP | Khi nào |
|---|---|---|
| `seating.input.invalid` | 400 | Dữ liệu gửi lên không hợp lệ. |
| `seating.idempotency.required` | 400 | Thiếu hoặc sai `Idempotency-Key`. |
| `seating.transfer.same_table` | 400 | Chuyển phiên sang chính bàn đang ngồi. |
| `seating.area.not_found`, `seating.table.not_found`, `seating.session.not_found` | 404 | Không có đối tượng. |
| `seating.area.code_conflict`, `seating.table.code_conflict` | 409 | Mã khu vực hoặc mã bàn đã dùng. |
| `seating.area.inactive`, `seating.table.inactive` | 409 | Đối tượng đã lưu trữ. |
| `seating.area.has_active_tables` | 409 | Lưu trữ khu vực khi còn bàn chưa lưu trữ. |
| `seating.table.occupied` | 409 | Bàn đã có phiên đang mở (mở hoặc chuyển tới bàn không trống). |
| `seating.table.in_use` | 409 | Lưu trữ bàn đang có phiên mở. |
| `seating.session.closed` | 409 | Phiên đã đóng. |
| `seating.session.open_orders` | 409 | Đóng phiên khi còn order mở. **Chưa xảy ra** cho đến khi Sales API cài `ISessionClosureGuard` (HIE-3.1). |
| `seating.concurrency.conflict` | 409 | `version` đã cũ. |
| `seating.idempotency.conflict` | 409 | Cùng khóa, khác nội dung. |
| `salesday.day.not_open` | 409 | Không có ngày bán đang mở. |

Danh sách khai báo trong `SeatingErrors` (`SeatingContracts.cs`); khi thêm mã mới phải cập nhật bảng này.
