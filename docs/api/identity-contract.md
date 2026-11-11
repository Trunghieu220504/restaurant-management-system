# Identity Authentication API Contract

Đặc tả hợp đồng API xác thực phiên làm việc cho hệ thống Quản lý Nhà hàng.

## 1. Nguyên tắc lưu trữ & bảo mật Token

* **Access Token**: Lưu trữ **trong bộ nhớ RAM (In-Memory)** phía client. Tuyệt đối không lưu vào `localStorage`, `sessionStorage` hay cookie mở để chống tấn công XSS. Thời hạn hiệu lực: 15 phút.
* **Refresh Token**: Lưu trữ trong **Cookie** với các cờ bảo mật:
  * `HttpOnly`: Trình duyệt và mã JavaScript client không thể đọc được giá trị cookie.
  * `SameSite=Strict` hoặc `Lax`: Chống tấn công CSRF.
  * `Path=/api/identity`: Cookie chỉ tự động gửi kèm khi client gọi đến các endpoint xác thực của module Identity, không gửi kèm ở các request tĩnh hoặc trang web thông thường.
  * `Max-Age=604800` (7 ngày).
* **Next.js Route Guard**: Vì cookie có `Path=/api/identity`, Next.js Middleware chạy tại edge/server khi tải các trang thông thường (như `/`, `/menu`, `/orders`) sẽ **không đọc được** cookie này. Do đó, cơ chế bảo vệ phân quyền (Route Guard) được xử lý ở **phía Client**:
  * Khi trang web mount, ứng dụng gửi request `POST /api/identity/refresh`.
  * Nếu thành công: khôi phục phiên, lưu access token vào bộ nhớ, lấy quyền và cho phép vào hệ thống.
  * Nếu thất bại: chuyển hướng người dùng về trang `/login`.

---

## 2. Các Endpoint API (`/api/identity`)

### 2.1. Đăng nhập (`POST /api/identity/login`)
* **Headers**: `Content-Type: application/json`
* **Request Body**:
  ```json
  {
    "userName": "nguyen the vinh",
    "password": "Password123!"
  }
  ```
* **Response 200 OK**:
  * **Headers**: `Set-Cookie: refreshToken=<token>; Path=/api/identity; HttpOnly; SameSite=Strict; Max-Age=604800`
  * **Body**:
    ```json
    {
      "accessToken": "ey...",
      "expiresAt": "2026-11-11T11:30:00Z",
      "account": {
        "id": "e4293f01-6b45-4299-8f0a-7098e9c8f001",
        "userName": "nguyen the vinh",
        "email": "vinh@restaurant.local",
        "roles": ["Manager"],
        "permissions": [
          "sales.orders.manage",
          "catalog.menu.manage",
          "identity.accounts.manage",
          "identity.subordinates.manage",
          "identity.roles.manage"
        ]
      }
    }
    ```
* **Response 400 Bad Request**: Dữ liệu gửi lên không đúng định dạng.
* **Response 401 Unauthorized**: Tên đăng nhập hoặc mật khẩu không chính xác.

---

### 2.2. Khôi phục / Làm mới Access Token (`POST /api/identity/refresh`)
* **Headers**: Không yêu cầu Authorization header; trình duyệt tự động gửi cookie `refreshToken`.
* **Request Body**: Rỗng.
* **Response 200 OK**:
  * **Body**:
    ```json
    {
      "accessToken": "ey...new_token",
      "expiresAt": "2026-11-11T11:45:00Z",
      "account": {
        "id": "e4293f01-6b45-4299-8f0a-7098e9c8f001",
        "userName": "nguyen the vinh",
        "email": "vinh@restaurant.local",
        "roles": ["Manager"],
        "permissions": [
          "sales.orders.manage",
          "catalog.menu.manage",
          "identity.accounts.manage",
          "identity.subordinates.manage",
          "identity.roles.manage"
        ]
      }
    }
    ```
* **Response 401 Unauthorized**: Không có cookie `refreshToken` hoặc refresh token đã bị thu hồi/hết hạn.

---

### 2.3. Lấy thông tin tài khoản hiện tại (`GET /api/identity/me`)
* **Headers**: `Authorization: Bearer <accessToken>`
* **Response 200 OK**:
  ```json
  {
    "id": "e4293f01-6b45-4299-8f0a-7098e9c8f001",
    "userName": "nguyen the vinh",
    "email": "vinh@restaurant.local",
    "roles": ["Manager"],
    "permissions": [
      "sales.orders.manage",
      "catalog.menu.manage",
      "identity.accounts.manage",
      "identity.subordinates.manage",
      "identity.roles.manage"
    ]
  }
  ```
* **Response 401 Unauthorized**: Access token thiếu, sai hoặc đã hết hạn.

---

### 2.4. Đăng xuất (`POST /api/identity/logout`)
* **Headers**: `Authorization: Bearer <accessToken>`
* **Response 204 No Content**:
  * **Headers**: `Set-Cookie: refreshToken=; Path=/api/identity; HttpOnly; SameSite=Strict; Max-Age=0`
  * Thu hồi session hiện tại trên máy chủ, xóa cookie refresh.

---

### 2.5. Đổi mật khẩu (`POST /api/identity/change-password`)
* **Headers**: `Authorization: Bearer <accessToken>`
* **Request Body**:
  ```json
  {
    "currentPassword": "Password123!",
    "newPassword": "NewPassword456!"
  }
  ```
* **Response 204 No Content**: Đổi mật khẩu thành công và thu hồi tất cả các session đang hoạt động.
* **Response 400 Bad Request**: Mật khẩu hiện tại sai hoặc mật khẩu mới vi phạm chính sách bảo mật.

---

## 3. Quy chuẩn xử lý Concurrency (Single-flight Refresh)

Khi access token hết hạn trong lúc người dùng đang thao tác:
1. Nhiều request API gửi đồng thời sẽ cùng nhận mã lỗi `401 Unauthorized`.
2. Hệ thống client **chỉ được gửi duy nhất 1 request `POST /api/identity/refresh`** lên server.
3. Các request 401 còn lại phải được xếp hàng chờ (Queue / await chung `refreshPromise`).
4. Khi request refresh thành công:
   * Cập nhật access token mới vào bộ nhớ.
   * Tất cả các request đang chờ được tự động gửi lại (Retry) với access token mới và trả kết quả thành công về cho UI.
5. Nếu refresh thất bại:
   * Tất cả request đang chờ bị từ chối với lỗi 401.
   * Xóa sạch token trong bộ nhớ và chuyển hướng người dùng về trang đăng nhập.
