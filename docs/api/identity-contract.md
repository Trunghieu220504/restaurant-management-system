# Hợp đồng API Identity (dành cho frontend)

Nguồn sự thật cho **các endpoint đã có** là [openapi.json](openapi.json). File này giải thích thêm ý nghĩa, ví dụ JSON và
mô tả refresh token của HIE-1.3. Chỉ bật frontend refresh sau khi migration và API này được triển khai.

- Đường dẫn gốc: `/api/identity`. Trên staging web và API cùng origin (qua Nginx), nên không cần CORS.
- Mọi response của Identity có `Cache-Control: no-store`.
- Xác thực: `Authorization: Bearer <accessToken>`. Access token (JWT) sống **15 phút**.
- Body request tối đa 16 KiB (vượt: `413`).

## 1. Đã có trong code (khớp `openapi.json`)

| Method | Đường dẫn | Xác thực | Request | Thành công | Lỗi |
|---|---|---|---|---|---|
| POST | `/login` | không | `LoginRequest` | `200 LoginResult` | 400, 401, 429 |
| GET | `/me` | Bearer | không | `200 CurrentAccount` | 401 |
| POST | `/logout` | Bearer hoặc cookie `rt` | không | `204` | 401, 429 |
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
Các endpoint quản lý tài khoản (mục 5) có trường `code`; các endpoint Identity khác (đăng nhập, đổi mật khẩu, refresh) **chưa có** `code` (SalesDay đã có). Frontend phân biệt theo mã HTTP; `title` chỉ để hiển thị gỡ lỗi, không so sánh chuỗi.

| Mã | Khi nào | Frontend xử lý |
|---|---|---|
| 400 | Dữ liệu sai hình dạng; đổi mật khẩu bị từ chối (sai mật khẩu hiện tại hoặc mật khẩu mới không đạt chính sách) | Hiện lỗi chung ở form; **không** tiết lộ trường nào sai |
| 401 | Đăng nhập sai (mọi lý do: sai mật khẩu, khóa, vô hiệu, không tồn tại, bật MFA đều cùng một lỗi); token hết hạn/thu hồi | Với `/login`: báo "sai tài khoản hoặc mật khẩu". Với API khác: thử refresh rồi gọi lại; nếu vẫn 401 thì về trang đăng nhập |
| 403 | Đăng nhập rồi nhưng thiếu quyền | Trang 403, không thử refresh |
| 413 | Body quá 16 KiB | Lỗi chung |
| 429 | Vượt giới hạn: đăng nhập 10 lần/phút theo IP; đổi mật khẩu 5 lần/phút theo người dùng | Báo thử lại sau, tạm khóa nút gửi |

Hành vi cần biết:
- `/logout` chỉ thu hồi **phiên hiện tại**. `/change-password` thu hồi **mọi phiên** của tài khoản (cả phiên đang đổi): sau khi đổi, frontend phải đưa người dùng về đăng nhập lại.
- Phiên được kiểm trong cơ sở dữ liệu ở mỗi request, nên token bị thu hồi sẽ bị từ chối ngay dù chưa hết 15 phút.
- Refresh cho phép cấp access token mới sau 15 phút; cookie hết hạn tuyệt đối 7 ngày sau đăng nhập.

## 2. Refresh token (HIE-1.3)

Xem `../identity-refresh-handoff.md` cho migration, giới hạn và bàn giao frontend.

Mục tiêu: người dùng không bị đá ra mỗi 15 phút, nhưng token dài hạn không nằm trong JavaScript.

### 2.1 Cookie refresh

- Tên: `rt`. Giá trị là chuỗi ngẫu nhiên; server chỉ lưu **hash**.
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
| POST | `/refresh` | cookie `rt` | không có body | `200 LoginResult` | 401, 429 |

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

Đường dẫn `/openapi/v1.json` đã được kiểm chứng ngày 10/11/2026. File xuất ra hiện gồm Identity (kể cả quản lý tài khoản), Seating ([seating-contract.md](seating-contract.md)), SalesDay và `/api/system/modules`.

## 5. Quản lý tài khoản (HIE-5.2)

Đường dẫn gốc `/api/identity/accounts`. Cần Bearer và một trong hai quyền: `identity.accounts.manage` (Admin) hoặc
`identity.subordinates.manage` (Manager). Khớp `openapi.json`.

| Method | Đường dẫn | Request | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/` (query: `search`, `role`, `active`, `offset`, `limit`) | không | `200 AccountPage` | 400, 401, 403 |
| GET | `/{id}` | không | `200 AccountDto` | 401, 403, 404 |
| POST | `/` | `CreateAccountRequest` | `201 AccountDto` + `Location` | 400, 401, 403, 409 |
| PUT | `/{id}/roles` | `SetRolesRequest` | `200 AccountDto` | 400, 401, 403, 404 |
| POST | `/{id}/activate` | không | `200 AccountDto` | 401, 403, 404 |
| POST | `/{id}/deactivate` | không | `200 AccountDto` | 401, 403, 404 |
| POST | `/{id}/reset-password` | `ResetPasswordRequest` | `204` | 400, 401, 403, 404 |

```text
AccountDto             { id: uuid, userName: string, email: string | null, roles: string[], active: bool, lockedOut: bool }
AccountPage            { items: AccountDto[], total: int }
CreateAccountRequest   { userName: string, email: string, password: string, roles: string[] }
SetRolesRequest        { roles: string[] }
ResetPasswordRequest   { newPassword: string }
```

- Vai trò hợp lệ: `Admin`, `Manager`, `Staff`, `Kitchen`, `Bar`. Một tài khoản phải có ít nhất một vai trò.
- Danh sách: `limit` mặc định 50, tối đa 200; `offset` ≥ 0; `search` khớp một phần tên đăng nhập hoặc email; `role` phải là vai trò hợp lệ.
- Mật khẩu do người tạo/đặt lại cung cấp, theo cùng chính sách như đổi mật khẩu (≥ 12 ký tự, hoa, thường, số, ký tự đặc biệt).
  Hệ thống **chưa** có cờ "bắt đổi mật khẩu ở lần đăng nhập đầu" (cần migration); người quản lý phải chuyển mật khẩu tạm qua kênh an toàn.
  Không có `Idempotency-Key` và không có giới hạn tốc độ riêng cho nhóm endpoint này.

### 5.1 Ai được làm gì

| Người gọi | Phạm vi tài khoản | Vai trò được gán |
|---|---|---|
| Admin | mọi tài khoản | cả năm vai trò |
| Manager | chỉ tài khoản mà **toàn bộ** vai trò thuộc {Staff, Kitchen, Bar} | chỉ Staff, Kitchen, Bar |

- Manager chạm vào tài khoản có Admin/Manager (kể cả tài khoản có cả Staff lẫn Manager) → `403 out_of_scope`; gán Admin/Manager → `403 role_not_allowed`.
- Không ai được đổi vai trò, khóa/mở khóa hoặc đặt lại mật khẩu **của chính mình** (`403 self_change`; đổi mật khẩu của mình dùng `/change-password`).
- Phạm vi của người gọi được đọc lại từ cơ sở dữ liệu trong từng thao tác ghi, không dựa vào claim trong token; người gọi bị vô hiệu hóa hoặc mất quyền → `403 forbidden`.
- Đổi vai trò có hiệu lực ngay với token đang dùng (vai trò được dựng lại mỗi request, xem test T17), nên **không** thu hồi phiên.
- Vô hiệu hóa và đặt lại mật khẩu **thu hồi mọi phiên và refresh token** của tài khoản. Đặt lại mật khẩu còn xóa khóa đăng nhập và bộ đếm đăng nhập sai.

### 5.2 Mã lỗi

ProblemDetails kèm trường `code` ổn định (frontend so sánh theo `code`, không theo `title`).

| `code` | HTTP | Khi nào |
|---|---|---|
| `identity.account.input_invalid` | 400 | Thiếu/sai hình dạng dữ liệu, vai trò không hợp lệ, tham số danh sách ngoài khoảng |
| `identity.account.password_invalid` | 400 | Mật khẩu không đạt chính sách |
| `identity.account.not_found` | 404 | Không có tài khoản này |
| `identity.account.forbidden` | 403 | Người gọi không (còn) có quyền quản lý tài khoản |
| `identity.account.out_of_scope` | 403 | Manager chạm vào tài khoản ngoài phạm vi |
| `identity.account.role_not_allowed` | 403 | Manager gán vai trò Admin/Manager |
| `identity.account.self_change` | 403 | Tự đổi vai trò/trạng thái/mật khẩu của mình |
| `identity.account.user_name_taken` | 409 | Tên đăng nhập đã tồn tại |
| `identity.account.email_taken` | 409 | Email đã tồn tại |

### 5.3 Đồng thời

Mọi thao tác ghi lấy khóa ứng dụng SQL `Restaurant.Identity.AccountAdministration` trước, rồi khóa theo người dùng
`Restaurant.Identity.User.<id>`. Vì các thao tác ghi được tuần tự hóa, đọc lại phạm vi người gọi trong giao dịch và cấm tự thay đổi,
hệ thống luôn còn ít nhất một Admin hoạt động: hai Admin cùng vô hiệu hóa nhau thì đúng một yêu cầu thành công. Vì vậy không có mã lỗi "last admin" riêng.
