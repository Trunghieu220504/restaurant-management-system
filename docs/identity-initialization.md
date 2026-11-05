# Khoi tao Identity

## Pham vi

InitialIdentity tao 7 bang ASP.NET Core Identity, Permission, RolePermission va
AdminBootstrap trong schema identity. Nguoi dung/vai tro dung Guid. Khong seed
tai khoan, mat khau, hash mat khau, refresh token hay thong tin ket noi.

Ma vai tro on dinh (ten hien thi): Admin (Quan tri), Manager (Quan ly),
Staff (Nhan vien), Kitchen (Bep), Bar (Bar). Policy SystemAdministration hien
co van nhan role Admin. Admin chi co identity.accounts.manage va
identity.roles.manage; Manager chi quan ly tai khoan cap duoi. Ma quyen la
hop dong cho cac use case sau, KHONG phai authorization handler da trien khai.
Login/me/logout/doi mat khau da duoc bo sung, xem identity-authentication.md.
Tao tai khoan qua API, refresh token va phan quyen nghiep vu chi tiet van chua co.

Danh muc quyen va mapping nam trong IdentityCatalog.cs; migration quan ly seed
bang ID co dinh. Chay API khong seed lai hay ghi de thay doi phan quyen.
Khong tu dong dong bo cac ma role cu nhu ADMIN/QUAN_LY vao role moi.

## Migration

Tu repo root, cau hinh ConnectionStrings__Restaurant qua secret manager hoac
bien moi truong cua process (khong commit vao appsettings). Restore tool va sinh
SQL de review truoc:

```bash
dotnet tool restore
dotnet ef migrations script --idempotent --project src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure --context IdentityDbContext --output artifacts/identity-initial.sql
```

Chi sau khi duyet SQL/backup va xac nhan database dich, nguoi van hanh moi chay:

```bash
dotnet ef database update --project src/Modules/Identity/Restaurant.Modules.Identity.Infrastructure --context IdentityDbContext
```

Khong chay tren database da duoc tao bang EnsureCreated/SQL cu ma chua doi chieu
migration history. Idempotent script dua vao migration history, khong tu sua
database bat ky. Khong dung Down tren database co tai khoan can giu lai.
API khong tu chay Migrate/EnsureCreated khi khoi dong.

## Tao Admin mot lan

Chi dung process CLI rieng, sau khi migration da ap dung. Khong can JWT secrets
vi lenh thoat truoc khi cau hinh web host. Chi nhan ba bien moi truong:
BootstrapAdmin__UserName, BootstrapAdmin__Email, BootstrapAdmin__Password.
Khong truyen mat khau trong command-line argument, file theo doi boi Git hay log.

Vi du PowerShell nhap tuong tac, khong ghi mat khau vao lich su lenh:

```powershell
$env:BootstrapAdmin__UserName = Read-Host 'Admin username'
$env:BootstrapAdmin__Email = Read-Host 'Admin email'
$secret = Read-Host 'Admin password' -AsSecureString
try {
    $env:BootstrapAdmin__Password = [System.Net.NetworkCredential]::new('', $secret).Password
    dotnet run --project src/Host/Restaurant.Api --no-launch-profile -- --bootstrap-admin
    if ($LASTEXITCODE -ne 0) { throw 'Bootstrap failed; inspect configuration, not secrets.' }
}
finally {
    Remove-Item Env:BootstrapAdmin__UserName -ErrorAction SilentlyContinue
    Remove-Item Env:BootstrapAdmin__Email -ErrorAction SilentlyContinue
    Remove-Item Env:BootstrapAdmin__Password -ErrorAction SilentlyContinue
    $secret.Dispose()
}
```

Password policy hien tai: toi thieu 12 ky tu, co chu hoa/thuong, so va ky tu dac
biet. Dung mat khau dai ngau nhien cua password manager. Email chua duoc danh dau
da xac minh. CLI chi bao thanh cong/that bai, khong in exception/user/connection.

Lenh lay SQL application lock, kiem tra marker va Admin hien co, tu choi username
hoac email da ton tai, tao account + role + marker trong mot transaction.
Chay lai bi tu choi; khong reset password, khong nang quyen tai khoan cu.
AdminBootstrap la dau vet bat bien cua lan khoi tao, khong thay the Audit module.
Khong xoa marker de tai su dung; tai khoan Admin ve sau phai qua quy trinh quan tri.

## Kiem thu va GitHub

Test model/script khong can SQL Server. IdentitySqlServerTests chi chay khi co
IDENTITY_TEST_SQLSERVER tro toi INSTANCE TEST RIENG co quyen tao database.
Moi test tao database RestaurantIdentityTest_<random>, chi xoa dung database do.
Tuyet doi khong cung cap instance production. CI tao SQL Server Docker tam voi
mat khau ngau nhien duoc che log, chay 6 test that va huy container sau job.

Test SQL gom migration lap lai, giu grant da sua, hash password, khong tao lai,
khong nang quyen account cu, bootstrap dong thoi, password yeu va rollback loi.
Test SQL khong chay local vi Docker daemon khong san sang. Tren GitHub PR #7,
hai job CI da thanh cong cho commit 9a2cfbf, gom 6 test SQL Server that trong
instance tam. Day khong phai migration hay tao tai khoan tren database nha hang.

Nhanh feature/identity-initialization ke thua chore/sync-salesday-docs, da vao main
qua PR #6 trong luc trien khai. develop van o README ban dau; PR vao develop se
bao gom ca nen backend va tai lieu de bootstrap nhanh nay theo CONTRIBUTING.md.
Khong tu dong merge, force push hay thay doi hai nhanh chinh.
