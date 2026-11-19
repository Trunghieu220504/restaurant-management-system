/*
    02-tao-tai-khoan-test.sql
    Nghiem thu PHU-1.2 / DUY-1.3 tren staging - chuan bi du lieu thu (phan viec cua Phuong).

    GHI DU LIEU: them tai khoan test vao identity.AspNetUsers va identity.AspNetUserRoles.
    Lam theo docs/huong-dan-tai-khoan-test.md (PR #48, cho Hai va Hieu duyet); script nay
    gop hai tai khoan vao mot transaction va tu chan ba loi da gap o muc 7 cua tai lieu do.
    Chi dung cho staging/may ca nhan, KHONG dung cho database co du lieu that.

    Truoc khi chay:
      1. Hai/Hieu dong y tao tai khoan; backup database theo quy trinh deploy.
      2. Tao chuoi bam cho tung mat khau theo muc 4 cua docs/huong-dan-tai-khoan-test.md
         (moi chuoi 84 ky tu, bat dau AQAAAAIAAYag, ket thuc ==).
      3. Chep file nay ra NGOAI repo, dan chuoi bam vao bang @TaiKhoan ben duoi.
         KHONG commit, KHONG dan vao PR/issue/chat ban da co chuoi bam.
    Sau khi chay: xoa ban sao co chuoi bam; ghi so tien do (thoi diem, nguoi tao), khong ghi mat khau.

    Tai khoan da ton tai thi script dung lai, khong doi mat khau hay vai tro cua tai khoan cu.
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;   -- bang Identity co chi muc loc: thieu dong nay (hoac sqlcmd -I) thi INSERT bi tu choi
GO
SET XACT_ABORT ON;

-- ===== THAM SO =====
DECLARE @DatabaseDich sysname = N'RestaurantManagement';   -- ten database staging da xac nhan
DECLARE @TaiKhoan TABLE (
    STT int IDENTITY(1, 1) PRIMARY KEY,
    Id uniqueidentifier NOT NULL DEFAULT NEWID(),
    UserName nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL,
    Email nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL,
    VaiTro nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL,
    PasswordHash nvarchar(400) COLLATE DATABASE_DEFAULT NOT NULL);
INSERT @TaiKhoan (UserName, Email, VaiTro, PasswordHash) VALUES
    (N'manager.test', N'manager.test@staging.invalid', N'Manager', N'<DAN_CHUOI_BAM_CUA_manager.test>'),
    (N'staff.test', N'staff.test@staging.invalid', N'Staff', N'<DAN_CHUOI_BAM_CUA_staff.test>');
-- Muon hai nguoi o ca 4 dung hai tai khoan Manager khac nhau thi them mot dong, vi du:
--  (N'manager2.test', N'manager2.test@staging.invalid', N'Manager', N'<DAN_CHUOI_BAM_CUA_manager2.test>'),
-- ===================

DECLARE @Loi nvarchar(2000);

IF DB_NAME() <> @DatabaseDich
BEGIN
    SET @Loi = CONCAT(N'Dang o database ', DB_NAME(), N', khong phai ', @DatabaseDich, N'. Khong co gi bi thay doi.');
    THROW 51000, @Loi, 1;
END;

IF COL_LENGTH(N'identity.AspNetUsers', N'IsActive') IS NULL
    THROW 51001, N'Thieu migration IdentityLoginSessions (cot IsActive). Khong co gi bi thay doi.', 1;

-- Kiem tra toan bo truoc khi ghi
SELECT @Loi = STRING_AGG(CONVERT(nvarchar(max), CONCAT(k.UserName, N': ', k.LyDo)), N'; ')
FROM (
    SELECT t.UserName,
        CASE
            WHEN t.PasswordHash LIKE N'<%' THEN N'chua dan chuoi bam'
            WHEN DATALENGTH(t.PasswordHash) <> 168
                THEN CONCAT(N'chuoi bam dai ', DATALENGTH(t.PasswordHash) / 2, N' ky tu, phai dung 84 (mat dau = hoac thua khoang trang?)')
            WHEN t.PasswordHash COLLATE Latin1_General_BIN LIKE N'%[^A-Za-z0-9+/=]%'
                THEN N'chuoi bam co ky tu la (\r, xuong dong, khoang trang...)'
            WHEN LEFT(t.PasswordHash, 12) COLLATE Latin1_General_BIN <> N'AQAAAAIAAYag'
                THEN N'chuoi bam khong bat dau bang AQAAAAIAAYag (khong phai PasswordHasher V3 cua API)'
            WHEN RIGHT(t.PasswordHash, 2) <> N'==' THEN N'chuoi bam khong ket thuc bang =='
            WHEN UPPER(t.VaiTro) = N'ADMIN' THEN N'script nay khong tao tai khoan Admin'
            WHEN NOT EXISTS (SELECT 1 FROM [identity].[AspNetRoles] r
                WHERE r.[NormalizedName] = UPPER(t.VaiTro) COLLATE DATABASE_DEFAULT)
                THEN CONCAT(N'khong co vai tro ', t.VaiTro)
            WHEN EXISTS (SELECT 1 FROM [identity].[AspNetUsers] u
                WHERE u.[NormalizedUserName] = UPPER(t.UserName) COLLATE DATABASE_DEFAULT
                    OR u.[NormalizedEmail] = UPPER(t.Email) COLLATE DATABASE_DEFAULT)
                THEN N'ten dang nhap hoac email da ton tai (script khong sua tai khoan cu)'
            WHEN (SELECT COUNT(*) FROM @TaiKhoan x
                WHERE UPPER(x.UserName) = UPPER(t.UserName) OR UPPER(x.Email) = UPPER(t.Email)) > 1
                THEN N'trung ten dang nhap/email trong danh sach'
            WHEN (SELECT COUNT(*) FROM @TaiKhoan x
                WHERE x.PasswordHash COLLATE Latin1_General_BIN = t.PasswordHash COLLATE Latin1_General_BIN) > 1
                THEN N'hai tai khoan dung cung mot chuoi bam (dan nham?)'
        END AS LyDo
    FROM @TaiKhoan t) k
WHERE k.LyDo IS NOT NULL;

IF @Loi IS NOT NULL
BEGIN
    SET @Loi = CONCAT(N'Khong tao tai khoan nao. ', @Loi);
    THROW 51002, @Loi, 1;
END;

DECLARE @SoDong int, @SoTaiKhoan int = (SELECT COUNT(*) FROM @TaiKhoan);

BEGIN TRANSACTION;
    INSERT [identity].[AspNetUsers] ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail],
        [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber],
        [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount])
    SELECT t.Id, t.UserName, UPPER(t.UserName), t.Email, UPPER(t.Email),
        0, t.PasswordHash, REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), CONVERT(nvarchar(36), NEWID()), NULL,
        0, 0, NULL, 1, 0
    FROM @TaiKhoan t;   -- IsActive lay mac dinh 1 tu migration
    SET @SoDong = @@ROWCOUNT;
    IF @SoDong <> @SoTaiKhoan
        THROW 51003, N'So tai khoan them vao khac du kien. Da huy toan bo.', 1;

    INSERT [identity].[AspNetUserRoles] ([UserId], [RoleId])
    SELECT t.Id, r.[Id]
    FROM @TaiKhoan t
    JOIN [identity].[AspNetRoles] r ON r.[NormalizedName] = UPPER(t.VaiTro) COLLATE DATABASE_DEFAULT;
    SET @SoDong = @@ROWCOUNT;
    IF @SoDong <> @SoTaiKhoan
        THROW 51004, N'So dong gan vai tro khac du kien. Da huy toan bo.', 1;
COMMIT TRANSACTION;

-- Kiem tra lai sau khi ghi: HashLength phai 84, IsActive 1, quyen dung vai tro
DECLARE @i int = 1, @Dong nvarchar(4000);
PRINT N'=== 02-tao-tai-khoan-test.sql: da tao ===';
WHILE @i <= @SoTaiKhoan
BEGIN
    SELECT @Dong = CONCAT(u.[UserName], N' | vai tro ', r.[Name], N' | IsActive ', u.[IsActive],
            N' | HashLength ', DATALENGTH(u.[PasswordHash]) / 2,
            N' | salesday.manage ', CASE WHEN EXISTS (SELECT 1 FROM [identity].[RolePermission] rp
                WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionCode] = 'salesday.manage') THEN N'CO' ELSE N'KHONG' END)
    FROM @TaiKhoan t
    JOIN [identity].[AspNetUsers] u ON u.[Id] = t.Id
    JOIN [identity].[AspNetUserRoles] ur ON ur.[UserId] = u.[Id]
    JOIN [identity].[AspNetRoles] r ON r.[Id] = ur.[RoleId]
    WHERE t.STT = @i;
    PRINT @Dong;
    SET @i += 1;
END;
PRINT N'Tiep theo: dang nhap thu tung tai khoan (POST /api/identity/login 200, /me dung quyen), chay lai 01.';
PRINT N'Xoa ban sao file co chuoi bam. Ghi so tien do: thoi diem tao, nguoi tao. Khong ghi mat khau.';
GO
