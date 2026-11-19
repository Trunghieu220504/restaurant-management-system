# Tạo tài khoản test cho E2E (tạm thời, đến khi có HIE-5.2)

> Trạng thái: **ĐỀ XUẤT, chờ Hải và Hiếu duyệt.** Cách làm đã được thử trên SQL Server 2022 cục bộ (đăng nhập thật qua API) vào 18/11/2026; **chưa chạy trên staging**.
> Người thực hiện trên staging: Hải (có quyền SSH vào VPS). Người duyệt: Hiếu.

## 1. Vì sao cần tài liệu này

Sáu ca E2E W1-E01 đến W1-E06 (`docs/week-one-acceptance.md`) cần **tài khoản test riêng** có quyền `salesday.manage`. Hiện:

- Lệnh `--bootstrap-admin` chỉ tạo **Admin** đầu tiên, và Admin **không** có `salesday.manage` (quyền này thuộc Manager, xem `IdentityCatalog.cs`).
- Chưa có API tạo tài khoản; API quản lý tài khoản (HIE-5.2) nằm ở tuần 5.

Vì vậy tạm thời cần thêm tài khoản Manager (và một Staff để kiểm tra chặn quyền) trực tiếp vào cơ sở dữ liệu. Đây là **giải pháp tạm cho môi trường staging và máy cá nhân**, không dùng cho production.

| Tài khoản | Vai trò | Dùng cho |
|---|---|---|
| `manager.test` | Manager | W1-E01 đến W1-E06 (mở ngày, refresh, logout, đổi mật khẩu) |
| `staff.test` | Staff | Kiểm tra bị từ chối khi mở ngày (không có `salesday.manage`) |

Admin đã tạo bằng bootstrap không cần tạo lại.

## 2. Nguyên tắc an toàn

- Mật khẩu **sinh ngẫu nhiên**, tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt. Không dùng lại mật khẩu thật của ai.
- **Không commit** mật khẩu, chuỗi băm hay file chứa chúng. Không dán mật khẩu vào PR, issue hay log CI.
- Giao mật khẩu cho người test qua kênh riêng (không qua kênh nhóm công khai).
- Tài khoản Manager có quyền thật trên staging (mở ngày, thu tiền, duyệt hoàn tiền...). Chỉ tạo khi cần test và **vô hiệu hóa ngay sau khi xong** (mục 6).
- **Sao lưu database trước khi chèn** (dùng cùng quy trình backup của lần deploy Seating), vì đây là thay đổi dữ liệu trực tiếp.
- Không chạy trên database có dữ liệu thật của nhà hàng.

## 3. Điều kiện trước

- Migration Identity đã áp dụng; Admin đã được bootstrap (bảng `identity.AspNetRoles` có các vai trò `Manager`, `Staff`).
- Có máy tin cậy cài **.NET SDK 10.0.300** (theo `global.json`) để băm mật khẩu.
- Biết tên container SQL Server, tên database và biến môi trường chứa mật khẩu `sa` trong compose staging. Các tên này **chưa được ghi ở đây**; Hải điền theo `infra/` thực tế.

## 4. Bước 1: tạo chuỗi băm mật khẩu (trên máy tin cậy)

Mật khẩu được băm bằng đúng `PasswordHasher` mà API dùng (định dạng V3), để `UserManager` kiểm tra được khi đăng nhập. Tạo file `hash.cs` **bên ngoài thư mục repo** (ví dụ `~/tmp/hash.cs`):

```csharp
#:sdk Microsoft.NET.Sdk.Web
using Microsoft.AspNetCore.Identity;

var password = Environment.GetEnvironmentVariable("HASH_INPUT");
if (string.IsNullOrEmpty(password)) { Console.Error.WriteLine("HASH_INPUT is empty"); return 1; }
var hasher = new PasswordHasher<IdentityUser<Guid>>();
var user = new IdentityUser<Guid>();
var hash = hasher.HashPassword(user, password);
if (hasher.VerifyHashedPassword(user, hash, password) == PasswordVerificationResult.Failed) { Console.Error.WriteLine("self-check failed"); return 2; }
Console.WriteLine(hash);
return 0;
```

Chạy (PowerShell), mật khẩu nhập tương tác, không nằm trong lịch sử lệnh:

```powershell
$secret = Read-Host 'Mật khẩu test' -AsSecureString
try {
    $env:HASH_INPUT = [System.Net.NetworkCredential]::new('', $secret).Password
    dotnet run hash.cs
} finally {
    Remove-Item Env:HASH_INPUT -ErrorAction SilentlyContinue
    $secret.Dispose()
}
```

Kết quả là một dòng Base64 dài **84 ký tự**, bắt đầu bằng `AQAAAAIAAYag` và kết thúc bằng `==` (hoặc `=`). Kiểm tra độ dài trước khi dùng. Gói NuGet `Microsoft.DotNet.ILCompiler` có thể in cảnh báo NU1903; đó là gói công cụ của chế độ chạy file `.cs`, không thuộc dự án.

## 5. Bước 2: chèn tài khoản vào database

Lưu SQL sau thành `create-test-account.sql` **bên ngoài repo**, thay bốn chỗ `<...>` bằng giá trị thật. Dán chuỗi băm **trực tiếp vào câu lệnh**, không truyền qua `sqlcmd -v` (xem mục 7).

```sql
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @Id uniqueidentifier = NEWID();
DECLARE @RoleId uniqueidentifier =
    (SELECT Id FROM [identity].AspNetRoles WHERE NormalizedName = UPPER(N'<VAI_TRO>'));  -- Manager hoặc Staff
IF @RoleId IS NULL THROW 51000, 'Role not found', 1;
IF EXISTS (SELECT 1 FROM [identity].AspNetUsers
           WHERE NormalizedUserName = UPPER(N'<TEN_DANG_NHAP>') OR NormalizedEmail = UPPER(N'<EMAIL>'))
    THROW 51001, 'User already exists', 1;
INSERT [identity].AspNetUsers
    (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash,
     SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
VALUES
    (@Id, N'<TEN_DANG_NHAP>', UPPER(N'<TEN_DANG_NHAP>'), N'<EMAIL>', UPPER(N'<EMAIL>'), 0, N'<CHUOI_BAM_84_KY_TU>',
     REPLACE(CONVERT(nvarchar(36), NEWID()), '-', ''), CONVERT(nvarchar(36), NEWID()), 0, 0, 1, 0);
INSERT [identity].AspNetUserRoles (UserId, RoleId) VALUES (@Id, @RoleId);
COMMIT;
```

Cột `IsActive` không cần điền; migration đặt mặc định là `1`.

Chạy trên VPS. Mật khẩu `sa` lấy từ biến môi trường **bên trong container**, không đặt trên dòng lệnh. Tên container và database do Hải điền:

```bash
docker exec -i <ten-container-sql> bash -c \
  '/opt/mssql-tools18/bin/sqlcmd -C -I -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d <ten-database>' \
  < create-test-account.sql
```

- Cờ `-I` **bắt buộc** (bật `QUOTED_IDENTIFIER`; thiếu thì INSERT bị từ chối, xem mục 7).
- Cờ `-b` làm lệnh trả mã lỗi khác 0 nếu SQL lỗi.
- Nếu container không có biến `MSSQL_SA_PASSWORD`, đừng gõ mật khẩu vào dòng lệnh; dùng cách khác mà nhóm đã dùng khi áp migration.
- Xóa `create-test-account.sql` sau khi chạy (file chứa chuỗi băm).

Kiểm tra:

```sql
SELECT u.UserName, r.Name AS Role, u.IsActive, LEN(u.PasswordHash) AS HashLength
FROM [identity].AspNetUsers u
JOIN [identity].AspNetUserRoles ur ON ur.UserId = u.Id
JOIN [identity].AspNetRoles r ON r.Id = ur.RoleId;
```

`HashLength` phải bằng **84** và `IsActive` bằng `1`.

## 6. Bước 3: kiểm tra đăng nhập và vô hiệu hóa sau test

Đăng nhập thật bằng `POST /api/identity/login`, rồi `GET /api/identity/me` với Bearer token. Trên máy cục bộ (18/11/2026) kết quả như sau:

| Tài khoản | Đăng nhập | `/me` trả về |
|---|---|---|
| Manager | 200 | 22 quyền, **có** `salesday.manage` |
| Staff | 200 | 9 quyền, **không có** `salesday.manage` |
| Manager, sai mật khẩu | 401 | |

Trên staging, đăng nhập sai quá 10 lần/phút/IP bị giới hạn (429); đừng thử sai nhiều lần.

**Vô hiệu hóa sau khi test xong** (mã nguồn từ chối đăng nhập khi `IsActive = 0`: `IdentityAuthentication.cs`, dòng 37 và 181; đã chạy thử cục bộ 18/11/2026: đăng nhập 200 trước, 401 sau khi vô hiệu hóa; chưa chạy trên staging):

```sql
UPDATE [identity].AspNetUsers SET IsActive = 0
WHERE NormalizedUserName IN (UPPER(N'manager.test'), UPPER(N'staff.test'));
```

Sau đó đăng nhập thử một lần để xác nhận trả 401. Ghi vào sổ tiến độ: thời điểm tạo, người tạo, thời điểm vô hiệu hóa. Không ghi mật khẩu.

## 7. Ba lỗi đã gặp khi thử (để khỏi lặp lại)

1. **`INSERT failed because the following SET options have incorrect settings: 'QUOTED_IDENTIFIER'`**: `sqlcmd` mặc định tắt `QUOTED_IDENTIFIER`, mà bảng Identity có chỉ mục lọc. Thêm cờ `-I`.
2. **Đăng nhập trả 500 `FormatException: not a valid Base-64 string`**: truyền chuỗi băm qua `sqlcmd -v PasswordHash=...` làm mất `=` ở cuối (84 ký tự còn 82). Dán chuỗi băm thẳng vào SQL và kiểm tra `HashLength = 84`. Triệu chứng đáng chú ý: lỗi 500 xảy ra **cả khi nhập sai mật khẩu**, vì ngoại lệ phát sinh trước bước so sánh.
3. **Ký tự `\r` cuối chuỗi băm** khi đọc kết quả trên Windows: loại bằng `tr -d '\r\n'` hoặc copy cẩn thận. Chuỗi hợp lệ chỉ gồm `A-Z a-z 0-9 + /` và `=` ở cuối.

## 8. Giới hạn

- Đây là cách làm **tạm**; sau HIE-5.2 phải tạo tài khoản qua API và bỏ tài liệu này.
- Cách chèn trực tiếp bỏ qua `UserManager` nên không chạy các kiểm tra của Identity (độ mạnh mật khẩu, trùng email theo quy tắc của thư viện). Mật khẩu phải tự bảo đảm theo mục 2.
- Không có dấu vết Audit cho thao tác này; vì vậy cần ghi tay vào sổ tiến độ.
- Đã kiểm chứng cục bộ: đúng dạng lệnh `docker exec ... bash -c ... < file.sql` ở mục 5 (container SQL Server 2022 dùng `MSSQL_SA_PASSWORD`). Chưa kiểm chứng: tên container, tên database và biến môi trường thật của staging.
