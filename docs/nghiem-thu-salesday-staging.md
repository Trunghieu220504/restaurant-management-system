# Nghiệm thu mở ngày bán trên staging: phần việc của Phương

> Trạng thái: **SẴN SÀNG CHẠY, CHƯA CHẠY TRÊN STAGING.** Các script đã qua bộ phân tích cú pháp T-SQL
> và được đối chiếu tên bảng, tên cột với migration trên `develop` `df6160c`. Chưa chạy trên SQL Server
> thật, nên hãy chạy 01 (chỉ đọc) trước. 02, 04 và 05 ghi dữ liệu, vì vậy cần Hải và Hiếu đồng ý giống
> [runbook tài khoản test](huong-dan-tai-khoan-test.md).

Phạm vi: nghiệm thu PHU-1.2 (SalesDay) và DUY-1.3 (màn mở ngày) trên `https://staging.quanlynh.id.vn`.
Phân công gồm ba người. Hải deploy và xác nhận phiên bản. Phương xác nhận database đã có migration
SalesDay, chuẩn bị dữ liệu thử và kiểm tra bản ghi ngày bán. Người kiểm giao diện đăng nhập bằng tài
khoản có `salesday.manage` để mở ngày. Tài liệu này là phần của Phương.

## 1. Đã đối chiếu trong mã nguồn (`develop` `df6160c`)

- SalesDay chỉ có migration `20261110074313_InitialSalesDay`. Migration tạo bảng `salesday.NgayBan` gồm
  9 cột, hai CHECK (`CK_NgayBan_Cash` cho tiền ≥ 0 và `CK_NgayBan_State`) và ba chỉ mục unique. Chỉ mục
  `IX_NgayBan_ActiveSlot` có bộ lọc `ActiveSlot IS NOT NULL`, nên database chỉ cho phép tối đa một ngày
  hoạt động, kể cả khi ghi không qua API. Mã SalesDay trên `develop` giống `main`.
- `POST /api/salesday/open` yêu cầu quyền `salesday.manage`, và chỉ vai trò Manager có quyền này. Admin
  và Staff không có. Khi thiếu quyền, API trả 403 trước khi chạy nghiệp vụ.
- Thao tác mở ngày lấy `sp_getapplock` toàn cục rồi mới kiểm tra. Nếu còn ngày hoạt động, API trả 409
  `salesday.previous_not_closed`. Nếu ngày gửi lên không lớn hơn mọi ngày đã có, API trả 409
  `salesday.date.invalid`.
- **Chưa có API chốt ngày.** Sau khi mở, ngày giữ trạng thái `DANG_BAN` cho đến khi có PHU-4.2/5.2, và
  không mở thêm được ngày nào. Trình tự ở mục 4 được sắp xếp theo giới hạn này.
- Bằng chứng gián tiếp ngày 18/11 ([week1-acceptance](reviews/week1-acceptance.md), mục 9):
  `GET /api/salesday/current` trên staging trả 200 với thân rỗng. Điều này cho thấy API đọc được
  `salesday.NgayBan` và lúc đó chưa có ngày hoạt động, nhưng chưa chứng minh lịch sử migration, CHECK hay
  chỉ mục. Script 01 kiểm tra các phần còn lại.
- Nhãn trạng thái ngày trên thanh trên cùng của web lấy từ dữ liệu mẫu trong `localStorage`
  (`web/src/lib/store/restaurant-context.tsx`), không gọi API. Khi nghiệm thu, chỉ đối chiếu banner bên
  trong trang `/sales-day`.

## 2. Bộ script `scripts/salesday-staging/`

| File | Loại | Dùng khi |
|---|---|---|
| `01-kiem-tra-migration.sql` | Chỉ đọc | Trước buổi test, sau khi tạo tài khoản, có thể chạy lại nhiều lần |
| `02-tao-tai-khoan-test.sql` | Ghi | Khi 01 báo thiếu `manager.test`/`staff.test`; cần duyệt và backup trước |
| `03-xac-nhan-ngay-ban.sql` | Chỉ đọc | Sau ca 2, 3, 4 và 5 |
| `04-hoan-tac-ngay-test.sql` | Ghi | Chỉ khi phải làm lại ca mở ngày và Hải, Hiếu đã đồng ý |
| `05-vo-hieu-hoa-tai-khoan-test.sql` | Ghi | Sau buổi nghiệm thu |

Mỗi dòng kết quả có một trong các nhãn `[DAT]`, `[KHONG DAT]`, `[CANH BAO]` hoặc `[THONG TIN]`. Nhãn
được viết không dấu để `sqlcmd` trong container hiển thị đúng. Nếu có dòng `KHONG DAT`, script kết thúc
bằng lỗi và `sqlcmd -b` trả mã khác 0. Mọi tham số nằm trong khối `THAM SO` ở đầu file.

## 3. Cách chạy trên VPS

SQL Server không mở cổng ra ngoài (`infra/compose.backend.yml`), nên chỉ chạy được qua `docker exec`
trên VPS, bởi người có quyền Docker. Theo [bàn giao của Hải](hai-week1-handoff.md), tài khoản deploy
hiện chưa có quyền này.

```bash
# Chép thư mục scripts/salesday-staging lên VPS, ví dụ ~/salesday-check, rồi chạy trên VPS:
cd ~/salesday-check
SQL=$(sudo docker ps --filter label=com.docker.compose.project=restaurant-staging \
  --filter label=com.docker.compose.service=sqlserver --format '{{.Names}}')
echo "$SQL"   # phải ra đúng một tên container

# Liệt kê database của ứng dụng (không in mật khẩu); mặc định là RestaurantManagement
sudo docker exec "$SQL" bash -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -h -1 -Q "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE database_id > 4"'

# Chạy một script, lưu kết quả và mã thoát
sudo docker exec -i "$SQL" bash -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -C -I -b -S localhost -U sa -d RestaurantManagement' \
  < 01-kiem-tra-migration.sql > ket-qua-01.txt 2>&1; echo "exit=$?"; cat ket-qua-01.txt
```

- Mật khẩu `sa` được lấy từ biến môi trường bên trong container qua `SQLCMDPASSWORD`, nên không xuất hiện
  trên dòng lệnh VPS hay trong log. Không chạy `printenv` hoặc `docker compose config`, vì các lệnh này in
  ra secret.
- `-I` bật `QUOTED_IDENTIFIER` (script cũng tự bật). `-b` trả mã khác 0 khi script báo lỗi. `-C` tin chứng
  chỉ tự ký của SQL Server nội bộ.
- Nếu database có tên khác `RestaurantManagement`, sửa `-d` trong lệnh và `@DatabaseDich` trong 02, 04, 05.
- Tham số của 03 sửa trực tiếp trong khối `THAM SO` (dùng `nano` trên VPS, hoặc sửa trên máy rồi chép lên).
- Nếu nhóm có đường kết nối SSMS hoặc Azure Data Studio đã được duyệt thì cũng chạy được; kết quả nằm ở
  tab Messages.

## 4. Trình tự buổi nghiệm thu

Vì chưa chốt được ngày, mỗi buổi chỉ mở được ngày một lần. Ngoài ra, giao diện chỉ hiện câu "Bạn không có
quyền mở ngày bán." khi chưa có ngày hoạt động. Do đó phần giao diện của ca 5 làm trước, và ca 4 được
chuẩn bị trước ca 1. Cách chuẩn bị này kiểm tra cùng quy tắc với ca W1-E03 trong
[week-one-acceptance](week-one-acceptance.md).

| Bước | Ai | Làm gì | Phương chạy | Kỳ vọng |
|---|---|---|---|---|
| 0 | Hải | Deploy, ghi commit và image ID, kiểm tra `/health/ready` | | 200 |
| 0 | Phương | Chạy 01. Nếu B4 báo thiếu tài khoản: tạo chuỗi băm theo mục 4 của [runbook tài khoản](huong-dan-tai-khoan-test.md), chạy 02, đăng nhập thử, rồi chạy lại 01 | 01 | Hai dòng `TONG` đều `DAT`; ghi lại số ở dòng C1 |
| 0 | Cả nhóm | Thống nhất ngày kinh doanh D (không sớm hơn ngày ở dòng C3, thường là hôm nay) và tiền đầu ngày T. Nên chọn số dễ nhận ra, ví dụ 1.250.000 | | |
| 1 | Người kiểm giao diện | **Ca 5 (giao diện):** đăng nhập `staff.test` (hoặc tài khoản Admin test, cũng không có quyền; khi đó thêm tên vào `@KhongCoQuyen` của 03), vào `/sales-day` | | Không có form; hiện "Bạn không có quyền mở ngày bán." |
| 2 | Người thứ hai | **Chuẩn bị ca 4:** dùng trình duyệt hoặc hồ sơ riêng (ẩn danh), đăng nhập `manager.test` (hoặc `manager2.test` nếu đã tạo), mở `/sales-day`, nhập D và T nhưng **chưa bấm** | | Form hiển thị |
| 3 | Người kiểm giao diện | **Ca 1:** đăng nhập `manager.test`, mở ngày với D và T | | `POST /open` trả 201, banner "ĐANG BÁN" |
| 4 | Phương | **Ca 2:** điền D, T và `@SoNgayTruocTest` (số ở C1) vào 03 rồi chạy | 03 lần 1 | `TONG` `DAT`; chép dòng `BAN GHI` (có Id và Version) |
| 5 | Người kiểm giao diện | **Ca 3:** tải lại trang (F5) | 03 lần 2, điền thêm `@IdDaGhiNhan` và `@VersionDaGhiNhan` | Giao diện không đổi; N9 `DAT` |
| 6 | Người thứ hai | **Ca 4:** bấm "Mở ngày bán" trên form đã mở sẵn | 03 lần 3 | Hiện "Đã có ngày bán chưa chốt. Không thể mở thêm ngày." rồi hiện đúng ngày đã mở; N10 và N11 `DAT`, Id không đổi |
| 7 | Người kiểm giao diện | **Ca 5 (API, tùy chọn):** gọi `POST /api/salesday/open` bằng token của `staff.test` (đoạn lệnh bên dưới) | 03 lần 4 | HTTP 403; N12 `DAT` |
| 8 | Phương | Sau buổi: chạy 05, đăng nhập thử tài khoản test, ghi biên bản | 05 | Đăng nhập trả 401 |

Access token chỉ sống 15 phút. Nếu bản web đang chạy chưa bật refresh (xem
[week-one-acceptance](week-one-acceptance.md)), hãy làm bước 2 đến bước 6 trong vòng 15 phút. Nếu không,
form của người thứ hai sẽ báo "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.", và lúc đó ca 4 chỉ còn
kiểm được qua API (một request mở mới phải nhận 409 `salesday.previous_not_closed`).

Ca 5 qua API được làm **sau** ca 1 vì lý do an toàn. Nếu quyền bị lỗi, request sẽ bị chặn bởi
`previous_not_closed` chứ không tạo ra ngày mới. Kiểm tra quyền chạy trước nghiệp vụ, nên kỳ vọng vẫn là
403. Đoạn lệnh PowerShell dưới đây không ghi mật khẩu vào lịch sử lệnh:

```powershell
$base = 'https://staging.quanlynh.id.vn'
$cred = Get-Credential -UserName 'staff.test' -Message 'Mat khau staff.test'
$body = @{ userName = $cred.UserName; password = $cred.GetNetworkCredential().Password } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post "$base/api/identity/login" -ContentType 'application/json' -Body $body
try {
    Invoke-RestMethod -Method Post "$base/api/salesday/open" -ContentType 'application/json' `
        -Headers @{ Authorization = "Bearer $($login.accessToken)"; 'Idempotency-Key' = [guid]::NewGuid().ToString() } `
        -Body '{"businessDate":"2026-11-19","openingCash":1250000}'
} catch { $_.Exception.Response.StatusCode.value__ }   # kỳ vọng 403
Remove-Variable body, login, cred
```

Nếu nhóm muốn hai người bấm **thật sự cùng lúc**, gộp bước 3 và bước 6 bằng cách đếm ngược. Việc này chỉ
làm được một lần và kết quả phải giống nhau: một bên nhận 201, bên còn lại nhận thông báo 409, và 03 chỉ
thấy một ngày. Trường hợp tranh chấp đồng thời thật đã có test tự động
`Opening_is_serialized_and_replay_does_not_create_another_day` chạy trên SQL Server. Bộ test SQL của
Phương đã đạt 9/9 trên CI theo [bàn giao của Hải](hai-week1-handoff.md) ngày 18/11.

## 5. Đọc kết quả

- **01:** A1–A6 `DAT` nghĩa là database đã có migration SalesDay đúng cấu trúc. B1–B4 và C2 `DAT` nghĩa là
  đã sẵn sàng test. Nếu C2 `KHONG DAT` (đang có ngày hoạt động): dừng lại, không tự chốt hay xóa ngày đó,
  báo Hải và Hiếu. Nếu C3 `CANH BAO`: nhập ngày từ giá trị C3 đưa ra, vì ngày mặc định trên giao diện sẽ bị
  từ chối.
- **03:** N1–N8 ứng với ca 1–2 (đúng ngày, tiền, người mở, trạng thái `DANG_BAN`, có `RequestKey`).
  N9 ứng với ca 3 (Id và Version không đổi, nghĩa là dữ liệu không mất và không bị ghi lại). N10–N11 ứng
  với ca 4 (ngày D chỉ có một bản ghi, cả buổi chỉ tạo thêm đúng một ngày). N12 ứng với ca 5. N13 xác nhận
  mọi ngày hiện có đều do tài khoản có quyền mở. Dòng `THONG TIN` cần đối chiếu bằng mắt.
- Nếu muốn nối thao tác trên giao diện với bản ghi, so `RequestKey` trong dòng `BAN GHI` với header
  `Idempotency-Key` của request `POST /api/salesday/open` trong DevTools.
- Khi có `KHONG DAT`: ghi FAIL kèm dòng kết quả vào biên bản, và không sửa dữ liệu để test "qua". Nếu phải
  làm lại, chỉ dùng 04 sau khi Hải và Hiếu đồng ý, rồi bắt đầu lại từ bước 0.

## 6. Biên bản

Ghi commit và image ID (do Hải cung cấp), tên database, người chạy SQL và thời gian. Không đưa mật khẩu,
token, cookie hay chuỗi băm vào biên bản. Id và `RequestKey` không phải bí mật.

| Ca | Kỳ vọng giao diện | Kỳ vọng SQL | Kết quả | Bằng chứng |
|---|---|---|---|---|
| Migration | Không áp dụng | 01: A1–A6 `DAT` | NOT_RUN | `ket-qua-01.txt` |
| 1. Mở ngày D, T | 201, banner "ĐANG BÁN" | 03: N1 `DAT` | NOT_RUN | Ảnh màn hình, log Network đã che bí mật |
| 2. Đúng ngày, tiền, người mở | Ngày D, tiền T | 03: N2–N8 `DAT` | NOT_RUN | Dòng `BAN GHI` |
| 3. Tải lại trang | Dữ liệu không đổi | 03: N9 `DAT` | NOT_RUN | Kết quả 03 lần 2 |
| 4. Hai người cùng mở | Một bên 201, bên kia thấy thông báo "Đã có ngày bán chưa chốt…" | 03: N10, N11 `DAT` | NOT_RUN | Kết quả 03 lần 3 |
| 5. Không có quyền | Không có form; API trả 403 | 03: N12 `DAT` | NOT_RUN | Ảnh màn hình, mã HTTP |

NOT_RUN và BLOCKED không được tính là PASS.

### Đối chiếu màn hình do Phương cung cấp ngày 20/11/2026

Màn hình **Ngày bán** hiển thị **ĐANG BÁN**, tiền đầu ngày **0 VND**, mở lúc **14:30 ngày 19/11/2026**. Ảnh không hiển thị URL, ID ngày, người mở hoặc phản hồi HTTP. Smoke staging của nhóm trước đó xác nhận đăng nhập và đọc ngày bằng Manager/Staff. [Biên bản của nhóm trên issue #70](https://github.com/Trunghieu220504/restaurant-management-system/issues/70)

Trên máy phát triển ngày 20/11, một database SQL Server 2022 tạm đã trả 201 khi Manager mở ngày, 409 `salesday.previous_not_closed` với khóa mới, và GET current trả 200 với ID ban đầu.

Giữ nguyên ngày đang bán trên staging. Để nghiệm thu ca ghi, đối chiếu log gốc của lần mở (201) với lần thử bằng khóa mới (409 `salesday.previous_not_closed`) và GET current cùng ID trước/sau. Nếu thiếu log 201, chờ lượt mở ngày được nhóm bố trí trên staging sạch. Dòng 201/409 trong biên bản chỉ chuyển sang PASS khi có bằng chứng đúng môi trường.

## 7. Sau nghiệm thu

- Chạy 05 để vô hiệu hóa `manager.test`/`staff.test`, rồi đăng nhập thử một lần để thấy 401. Xóa bản sao
  02 có chuỗi băm.
- Ngày test vẫn ở trạng thái `DANG_BAN` vì chưa có API chốt ngày. Nhóm quyết định giữ ngày này cho Sales
  (tuần 2–3 cần ngày đang mở) hoặc hoàn tác bằng 04. Không "chốt" bằng cách sửa trực tiếp `Status`.

## 8. Giới hạn

- Script chưa được chạy trên SQL Server thật. Nếu gặp lỗi cú pháp hoặc sai tên cột, sửa script, không sửa
  database.
- SQL Server tự viết lại biểu thức CHECK, nên 01 kiểm tra định nghĩa theo từ khóa và in nguyên văn để
  người duyệt đọc lại.
- Bộ script không thay thế `PhuongWeekOneSqlServerTests` (tranh chấp đồng thời thật) hay các ca E2E
  W1-E01…E06.
- 02 ghi thẳng vào bảng Identity, bỏ qua `UserManager` giống runbook #48: không có audit và phải ghi tay
  vào sổ tiến độ.
