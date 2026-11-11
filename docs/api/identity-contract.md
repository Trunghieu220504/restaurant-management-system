# Hợp đồng API Identity (dành cho frontend)

Nguồn sự thật cho **các endpoint đã có** là [openapi.json](openapi.json). File này giải thích thêm ý nghĩa, ví dụ JSON và
mô tả **bản nháp** những phần chưa có code (refresh token). Phần nào ghi *(NHÁP)* có thể đổi cho đến khi PR HIE-1.3 merge;
frontend dùng nó để viết mock (MSW) nhưng không được xem là đã triển khai.

- Đường dẫn gốc: `/api/identity`. Trên staging web và API cùng origin (qua Nginx), nên không cần CORS.
- Mọi response của Identity có `Cache-Control: no-store`.
- Xác thực: `Authorization: Bearer <accessToken>`. Access token (JWT) sống **15 phút**.
- Body request tối đa 16 KiB (vượt: `413`).

## 1. Đã có trong code (khớp `openapi.json`)

| Method | Đường dẫn | Xác thực | Request | Thành công | Lỗi |
|---|---|---|---|---|---|
| POST | `/login` | không | `LoginRequest` | `200 LoginResult` | 400, 401, 429 |
| GET | `/me` | Bearer | không | `200 CurrentAccount` | 401 |
| POST | `/logout` | Bearer | không | `204` | 401 |
| POST | `/change-password` | Bearer | `ChangePasswordRequest` | `204` | 400, 401, 429 |

### 1.1 Kiểu dữ liệu

```text
LoginRequest           { userName: string, password: string }
ChangePasswordRequest  { currentPassword: string, newPassword: string }
LoginResult            { accessToken: string, expiresAt: ISO-8601 UTC, account: CurrentAccount }
CurrentAccount         { id: uuid, userName: string, email: string | null, roles: string[], permissions: string[] }
```

- `userName`: không rỗng, tối đa 256 ký tự, **không được có khoảng trắng đầu/cuối**.
- Mật khẩu: không rỗng, tối đa 1024 ký tự. Mật khẩu mới phải đạt chính sách: tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.
- `roles`: một trong `Admin`, `Manager`, `Staff`, `Kitchen`, `Bar`.
- `permissions`: mã quyền dạng `salesday.manage`, `catalog.menu.manage`, `seating.sessions.manage`... Menu và nút trên giao diện nên
  ẩn/hiện theo `permissions`, không theo `roles`. Ẩn nút chỉ là tiện lợi; backend vẫn kiểm quyền.

### 1.2 Ví dụ

```http
POST /api/identity/login
Content-Type: application/json

{ "userName": "quanly01", "password": "…" }
```

```json
{
  "accessToken": "eyJ…",
  "expiresAt": "2026-11-10T09:15:00Z",
  "account": {
    "id": "0b3c9f0e-7d5a-4c0e-9a55-1f1c2d3e4f50",
    "userName": "quanly01",
    "email": null,
    "roles": ["Manager"],
    "permissions": ["salesday.manage", "catalog.menu.manage"]
  }
}
```

Ví dụ trên chỉ minh họa hình dạng; giá trị là dữ liệu giả.

### 1.3 Lỗi hiện tại

Lỗi trả theo `application/problem+json` (ProblemDetails: `type`, `title`, `status`, `detail`, `instance`).
**Identity chưa có trường `code`** (SalesDay đã có). Frontend phân biệt theo mã HTTP; `title` chỉ để hiển thị gỡ lỗi, không so sánh chuỗi.

| Mã | Khi nào | Frontend xử lý |
|---|---|---|
| 400 | Dữ liệu sai hình dạng; đổi mật khẩu bị từ chối (sai mật khẩu hiện tại hoặc mật khẩu mới không đạt chính sách) | Hiện lỗi chung ở form; **không** tiết lộ trường nào sai |
| 401 | Đăng nhập sai (mọi lý do: sai mật khẩu, khóa, vô hiệu, không tồn tại, bật MFA đều cùng một lỗi); token hết hạn/thu hồi | Với `/login`: báo "sai tài khoản hoặc mật khẩu". Với API khác: thử refresh *(NHÁP)* rồi gọi lại; nếu vẫn 401 thì về trang đăng nhập |
| 403 | Đăng nhập rồi nhưng thiếu quyền | Trang 403, không thử refresh |
| 413 | Body quá 16 KiB | Lỗi chung |
| 429 | Vượt giới hạn: đăng nhập 10 lần/phút theo IP; đổi mật khẩu 5 lần/phút theo người dùng | Báo thử lại sau, tạm khóa nút gửi |

Hành vi cần biết:
- `/logout` chỉ thu hồi **phiên hiện tại**. `/change-password` thu hồi **mọi phiên** của tài khoản (cả phiên đang đổi): sau khi đổi, frontend phải đưa người dùng về đăng nhập lại.
- Phiên được kiểm trong cơ sở dữ liệu ở mỗi request, nên token bị thu hồi sẽ bị từ chối ngay dù chưa hết 15 phút.
- Hiện **chưa có refresh**: hết 15 phút phải đăng nhập lại cho tới khi HIE-1.3 xong.

## 2. Refresh token *(NHÁP — chưa có code)*

Mục tiêu: người dùng không bị đá ra mỗi 15 phút, nhưng token dài hạn không nằm trong JavaScript.

### 2.1 Cookie refresh

- Tên (đề xuất): `rt`. Giá trị là chuỗi ngẫu nhiên; server chỉ lưu **hash**.
- Thuộc tính: `HttpOnly; Secure; SameSite=Strict; Path=/api/identity`.
- Frontend **không đọc được** cookie này và **không** lưu access token hay refresh token ở `localStorage`/`sessionStorage`.
  Access token giữ trong bộ nhớ của trang.
- Vì cookie chỉ gửi kèm đường dẫn `/api/identity`, middleware của Next.js **không thấy** nó. Route guard phải làm phía client:
  khi tải trang, gọi `/refresh` để khôi phục phiên rồi mới quyết định cho vào hay chuyển về đăng nhập.
- Trên môi trường phát triển HTTP (`localhost`), cờ `Secure` có thể khiến trình duyệt không lưu cookie ở một số cấu hình;
  cách chạy cục bộ sẽ được ghi trong PR HIE-1.3. Với MSW, mock không cần cookie thật.

### 2.2 Endpoint

| Method | Đường dẫn | Xác thực | Request | Thành công | Lỗi |
|---|---|---|---|---|---|
| POST | `/refresh` *(NHÁP)* | cookie `rt` | không có body | `200 RefreshResult` | 401, 429 |

```text
RefreshResult { accessToken: string, expiresAt: ISO-8601 UTC, account: CurrentAccount }
```

Hình dạng giống `LoginResult` để frontend dùng chung một hàm lưu phiên.

Hành vi:
- Mỗi lần gọi thành công, server **xoay vòng**: cấp cookie `rt` mới, cookie cũ không dùng lại được.
- Dùng lại cookie đã xoay (dấu hiệu bị đánh cắp) ⇒ `401` và **thu hồi cả chuỗi**; người dùng phải đăng nhập lại.
- `/login` đặt cookie `rt`. `/logout` và `/change-password` thu hồi và xóa cookie.
- Thiếu cookie, cookie hết hạn hoặc bị thu hồi ⇒ `401`.

### 2.3 Quy tắc phía frontend

1. Khi một request nhận `401` từ API nghiệp vụ, gọi `/refresh` **một lần duy nhất**; các request khác cùng bị 401 chờ kết quả đó (không gọi song song).
2. Refresh thành công: lưu access token mới, gọi lại request ban đầu **một lần**. Nếu vẫn 401 hoặc refresh trả 401: xóa phiên, về trang đăng nhập.
3. Không tự gọi refresh cho `/login` hay `/refresh`.
4. Gọi `/refresh` khi tải trang để khôi phục phiên (tương ứng cookie còn hạn).

## 3. Quy ước dùng chung (tham khảo; sẽ chốt ở ADR-01)

- **`Idempotency-Key`**: header UUID cho các thao tác ghi quan trọng (ví dụ `POST /api/salesday/open`). Cùng khóa + cùng nội dung
  trả lại kết quả cũ; khác nội dung trả `409`. Frontend sinh một khóa mới cho mỗi *ý định* của người dùng và giữ nguyên khi thử lại.
- **Mã lỗi `code`**: lỗi nghiệp vụ của các module sau Identity dùng ProblemDetails kèm `code` ổn định (ví dụ `salesday.input.invalid`);
  frontend so sánh theo `code`, không theo `title`.
- Tiền VND là số nguyên (không số lẻ). Trong `openapi.json` kiểu này xuất hiện là `integer | string` (định dạng `int64`):
  parse thành số/BigInt tùy công cụ sinh client.

## 4. Cách tạo lại `openapi.json`

OpenAPI chỉ được mở ở môi trường Development (staging không có). Khi đổi endpoint, tạo lại và commit **trong cùng PR**:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__Restaurant = "<chuỗi kết nối bất kỳ; việc xuất không kết nối cơ sở dữ liệu>"
$env:Jwt__SigningKey = "<chuỗi ngẫu nhiên ≥ 32 ký tự, chỉ để xuất file, không phải khóa thật>"
dotnet run --project src/Host/Restaurant.Api --launch-profile http
# cửa sổ khác:
Invoke-WebRequest http://localhost:5080/openapi/v1.json -OutFile docs/api/openapi.json
```

Đường dẫn `/openapi/v1.json` đã được kiểm chứng ngày 10/11/2026. File xuất ra hiện gồm Identity, SalesDay và `/api/system/modules`.
