# Bộ kiểm chứng tuần 1 tự động

Phạm vi: kiểm chứng và bằng chứng tuần 1; không deploy lại R8, migrate staging, bootstrap Admin hoặc reset ngày bán. Kết quả cục bộ và staging được ghi riêng, không dùng mock để kết luận E2E thật.

## Chạy cục bộ trên Windows

Điều kiện: .NET SDK theo [global.json](../global.json), Node/npm, dependencies của frontend và một SQL Server 2022+ cục bộ đang chạy. [Bộ chọn SQL](../scripts/Get-WeekOneLocalSql.ps1) ưu tiên instance mặc định 2022+, rồi Developer/Express đang chạy; không thay dịch vụ, giao thức mạng hoặc cấu hình SQL. Có thể đặt `IDENTITY_TEST_SQLSERVER` qua môi trường riêng. Chỉ cho phép loopback/instance cục bộ, không chấp nhận tên database ứng dụng.

Tại PowerShell trong repository:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-WeekOne.ps1 -Mode Local
```

[Bộ chạy chính](../scripts/Test-WeekOne.ps1) restore locked, chạy test solution với SQL thật, kiểm tra TRX không có failure/skip, chạy unit test frontend, build frontend và cài Chromium đúng phiên bản Playwright nếu thiếu. [Host riêng](../scripts/WeekOneBrowserHost.cs) tạo database `RestaurantWeekOneBrowser_<GUID>`, migrate **database mới này**, tạo tài khoản bằng UserManager, rồi chỉ lắng nghe loopback. Không dùng database của caller. Sau test, host chỉ xóa database do chính nó tạo và xóa session file. Tên đích phải khớp chính xác trước cleanup; lỗi cleanup khiến báo cáo FAIL. Host có giới hạn thời gian 45 phút để dọn khi caller mất kết nối.

[Runsettings cục bộ](../scripts/week-one-local.runsettings) giới hạn hai collection song song để giảm tranh tài nguyên khi CREATE DATABASE trên máy phát triển; kết nối test có command timeout 120 giây. Các phép đồng thời Task.WhenAll, nhiều DbContext và các timeout/khóa transaction **bên trong từng test vẫn giữ nguyên**. Không đổi cấu hình CI hoặc code nghiệp vụ để che lỗi.

[Bộ trình duyệt thật](../web/scripts/verify-week-one.mjs) không route-mock, không đổi đồng hồ, không ghi HAR/trace/token/cookie vào báo cáo:

| Ca | Kiểm chứng |
|---|---|
| W1-E01 | Login UI 200, me thật 200, Manager có salesday.manage, cookie refresh thật |
| W1-E02 | Chưa có ngày hoạt động → mở qua UI 201, DANG_BAN |
| W1-E03 | Context thứ hai đã tải form trước đó, khóa mới → 409, ID ngày không đổi |
| W1-E04 | Chờ JWT hết hạn thật và clock skew; quan sát 401 → refresh 200 → retry 200, vẫn đăng nhập |
| W1-E05 | Logout UI 204; access/cookie cũ bị server từ chối 401 |
| W1-E06 | Đổi mật khẩu 204; access/cookie của context khác bị thu hồi, UI về login; mật khẩu cũ sai/mới đúng |
| INF10-lockout | Tài khoản riêng: đúng 200, sai 5 lần 401, đúng sau đó vẫn 401; tách cửa sổ rate limit, không nhầm với 429 |

Bộ chạy dừng nếu đã có ngày hoạt động trước W1-E02; không đóng/xóa/reset để ép test đạt. Thời gian dự kiến hơn 17 phút do JWT 15 phút, clock skew và cửa sổ login limiter.

Để lặp spike sau khi bộ test đã build thành công:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-WeekOneSpike.ps1
```

[Script spike](../scripts/Test-WeekOneSpike.ps1) chạy ít nhất 20 lượt, mỗi lượt phải đúng 9/9 test Phương, không skip; giữ TRX riêng từng lượt. Không thay thế test chốt ngày nghiệp vụ tuần 5.

[Workflow kiểm tra script](../.github/workflows/week-one-qa.yml) chạy trên PR liên quan hoặc workflow_dispatch: parse script, kiểm tra guard, compile ba helper C#, rồi chạy chính script spike 20 lượt với SQL 2022 Docker riêng. Workflow không truy cập staging, không quét cổng và không dùng tài khoản staging. TRX và summary được giữ trong artifact để kiểm tra số test thực sự chạy.

## Kiểm tra staging không đăng nhập

[Script công khai](../web/scripts/verify-staging-public.mjs) dùng Chromium thật để kiểm tra login/refresh build flag, redirect route bảo vệ, API 401 và HTTPS/HSTS. Không cần tài khoản và không ghi SalesDay:

```powershell
$env:WEEK1_REPORT_FILE = Join-Path (Get-Location) 'artifacts/hai-week1/staging-public-browser.json'
node web/scripts/verify-staging-public.mjs
Remove-Item Env:WEEK1_REPORT_FILE
```

## Chuẩn bị E2E staging — cần sudo và xác nhận dữ liệu thử

Đây là bước **chưa thực hiện** khi chưa có người vận hành xác nhận dữ liệu. Không dùng tài khoản cá nhân, bootstrap Admin hoặc mật khẩu thật. [Generator](../scripts/PrepareWeekOneCredentials.cs) sinh mật khẩu ngẫu nhiên, hash bằng PasswordHasher của Identity và tự kiểm tra hash; payload riêng được lưu ngoài Git. Tên ba tài khoản bắt đầu `w1-e2e-`, vai trò Manager/Staff/Staff riêng cho lockout.

Agent đã sinh payload được bảo vệ trên Windows, tải lên VPS mode 600 owner deploy và tải [script chuẩn bị](../scripts/restaurant-week1-prepare-e2e.sh). Không in payload, password/hash hoặc gửi chúng vào chat. Script snapshot payload sang file tạm root-owned trước khi kiểm tra/đọc, tránh thay đổi payload giữa validation và SQL.

Chỉ chạy tại prompt SSH Linux `deploy@restaurant-staging:~$`:

```bash
sudo install -o root -g root -m 0700 /home/deploy/restaurant-week1-prepare-e2e.sh /usr/local/sbin/restaurant-week1-prepare-e2e
sudo --preserve-env=SSH_CONNECTION /usr/local/sbin/restaurant-week1-prepare-e2e --plan
```

Plan kiểm tra Match SSH theo kết nối thực tế (deploy và root, không in địa chỉ), image staging đã biết, schema/roles và không trùng tài khoản. Chỉ nếu plan đạt **và staging hoàn toàn là dữ liệu thử được phép dùng**, chạy:

```bash
sudo --preserve-env=SSH_CONNECTION /usr/local/sbin/restaurant-week1-prepare-e2e --prepare-test-data
```

Apply tạo backup SQL COPY_ONLY/CHECKSUM, VERIFYONLY, sao chép backup vào thư mục root-only và ghi checksum, rồi chèn ba tài khoản trong transaction. Đây không phải bằng chứng restore thử trên instance khác. Không migrate, cấp Admin hoặc thay SalesDay. Cấp sudo chỉ cho helper root-owned **không đối số** vô hiệu hóa đúng ba UUID/tên vừa tạo; không cấp docker/shell sudo chung. Nếu chuẩn bị lỗi sau insert, script cố vô hiệu hóa đúng ba ID và giữ log riêng để kiểm tra.

Sau khi người vận hành hoàn tất, agent chạy từ PowerShell với session file riêng đã sinh:

```powershell
$env:WEEK1_SESSION_FILE = Join-Path (Get-Location) 'artifacts/hai-week1/staging-session.json'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-WeekOne.ps1 -Mode Staging -TestDataConfirmed
```

Runner chỉ cho phép đúng hostname staging và tài khoản có prefix riêng. Sau test thành công **hoặc thất bại**, nó gọi helper cleanup đã giới hạn, kiểm tra Manager/Staff dùng mật khẩu hiện tại bị từ chối 401 và xóa session files khi cleanup đạt. Nếu cleanup lỗi, báo FAIL và giữ dữ liệu cần thiết để vận hành xử lý; không đánh dấu Done. Payload phải được tạo mới cho một đợt mới; script chuẩn bị một lần dừng nếu helper/sudoers đã tồn tại, cần người vận hành review vòng đời trước khi thay.

## Quét cổng bên ngoài

[Scanner](../scripts/ScanWeekOnePorts.cs) chỉ cho phép đúng VPS staging đã biết, kiểm tra DNS trước chạy, tối đa 50 probe mới/giây và 64 socket đang chờ. Nó kiểm tra health định kỳ; lỗi health thì hủy probe và ghi FAIL_OR_INCOMPLETE. PASS chỉ khi đủ 1–65535 TCP IPv4, chỉ thấy 22/80/443 và health trước/sau đạt.

Đã chạy trên runner GitHub riêng tại [run 44327552913](https://github.com/Hai249/restaurant-management-system/actions/runs/44327552913), không dùng IP quản trị SSH làm nguồn. Không tự kích hoạt scanner trên CI/PR thông thường. Kết quả không phải quét UDP/IPv6 hoặc xác nhận nguồn 4G.

## Báo cáo và giới hạn

Báo cáo JSON đã lọc nằm trong `artifacts/hai-week1/automation-<UTC>/`, TRX/log chi tiết giữ cục bộ và không công khai khi chưa lọc. Status FAIL/BLOCKED không được đổi thành PASS bằng cách bỏ test, giảm assertion, tắt limiter hoặc sửa dữ liệu ngày bán. [Bảng bằng chứng](evidence/week-one/automation-2026-11-19.md) tách kết quả đạt và điều kiện còn thiếu. Các PR vẫn phải có CI và review độc lập theo quy trình repository.
