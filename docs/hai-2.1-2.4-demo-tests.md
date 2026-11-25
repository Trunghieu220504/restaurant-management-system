# HAI-2.1 / HAI-2.4 — dữ liệu demo và SQL test riêng

Nhánh: `feature/hai-2.1-2.4-demo-tests`. Lệnh `--seed-demo` tạo 5 tài khoản (`demo.admin`, `demo.manager`, `demo.staff`, `demo.kitchen`, `demo.bar`), một khu vực, hai bàn, món bếp và món bar, topping, hai nguyên liệu và ngày bán. Chạy lại không tạo trùng hoặc đổi mật khẩu tài khoản đã có.

Chỉ chấp nhận database tên `RestaurantDemo_<GUID 32 ký tự>` và environment Development/Testing/Staging. Không dùng connection string database ứng dụng. Database có sẵn mà thiếu marker `dbo.HaiDemoSeed` bị từ chối cả seed lẫn reset; Production luôn bị từ chối. `--reset-demo` xóa **toàn bộ database demo đã đánh dấu**, không xóa riêng vài dòng trong database dùng chung.

Ví dụ PowerShell với SQL Server local đã xác minh:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$demoDatabase = 'RestaurantDemo_' + [guid]::NewGuid().ToString('N')
$env:ConnectionStrings__Restaurant = (& ./scripts/Get-WeekOneLocalSql.ps1) + ';Database=' + $demoDatabase
$demoSecret = Read-Host 'Mật khẩu cho các tài khoản demo' -AsSecureString
$env:DemoSeed__Password = [Net.NetworkCredential]::new('', $demoSecret).Password
try {
    dotnet run --project src/Host/Restaurant.Api --no-launch-profile -- --seed-demo
    if ($LASTEXITCODE) { throw 'Demo seed failed' }
} finally {
    Remove-Item Env:DemoSeed__Password
}
# Giữ connection string riêng để chạy API vào đúng demo này.
# Chỉ chạy dòng sau khi muốn xóa database demo trên:
# dotnet run --project src/Host/Restaurant.Api --no-launch-profile -- --reset-demo
```

Không in hoặc commit mật khẩu/connection string. Để mở demo bằng UI, API và web phải được chạy cùng cấu hình; việc seed thành công không có nghĩa đã deploy staging.

SQL test một lệnh trên Windows/Linux (cần Python 3, .NET SDK và Docker):

```powershell
python scripts/release/test_sql_local.py
```

Runner tạo SQL Server 2022 container riêng, chỉ bind loopback bằng cổng ngẫu nhiên, restore/build/test với fixture tuần tự nhưng giữ tranh chấp đồng thời bên trong test. Container và volume test được dọn trong `finally`; kết quả TRX/JSON ở `artifacts/hai-sql-*`. Không có kết quả/skip/fail sẽ không được báo PASS. Có `--filter` và `--no-build` cho kiểm thử có mục tiêu sau khi đã build đúng source.

Bằng chứng test gồm: từ chối database ứng dụng và Production; seed hai lần giữ đúng 5 user/2 bàn/2 món/2 nguyên liệu; reset thành công database có marker; từ chối reset database chưa đánh dấu; toàn bộ dưới 5 phút. Không sử dụng dữ liệu staging thật để chứng minh.
