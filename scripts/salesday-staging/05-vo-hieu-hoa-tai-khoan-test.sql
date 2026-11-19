/*
    05-vo-hieu-hoa-tai-khoan-test.sql
    Nghiem thu PHU-1.2 / DUY-1.3 tren staging - don dep sau buoi nghiem thu.

    GHI DU LIEU: dat IsActive = 0 cho tai khoan test (muc 6 cua docs/huong-dan-tai-khoan-test.md).
    API kiem tra IsActive o moi request, khi dang nhap va khi refresh, nen phien dang mo
    cung bi tu choi ngay. Khong xoa tai khoan (salesday.NgayBan.OpenedBy van tro toi duoc).
    Script tu chan neu danh sach co tai khoan thuoc vai tro Admin.
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET QUOTED_IDENTIFIER ON;
GO
SET XACT_ABORT ON;

-- ===== THAM SO =====
DECLARE @DatabaseDich sysname = N'RestaurantManagement';   -- ten database staging da xac nhan
DECLARE @TaiKhoan TABLE (UserName nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
INSERT @TaiKhoan (UserName) VALUES (N'manager.test'), (N'staff.test');
-- ===================

DECLARE @Loi nvarchar(2000);

IF DB_NAME() <> @DatabaseDich
BEGIN
    SET @Loi = CONCAT(N'Dang o database ', DB_NAME(), N', khong phai ', @DatabaseDich, N'. Khong co gi bi thay doi.');
    THROW 51030, @Loi, 1;
END;

SELECT @Loi = STRING_AGG(u.[UserName], N', ')
FROM @TaiKhoan t
JOIN [identity].[AspNetUsers] u ON u.[NormalizedUserName] = UPPER(t.UserName) COLLATE DATABASE_DEFAULT
JOIN [identity].[AspNetUserRoles] ur ON ur.[UserId] = u.[Id]
JOIN [identity].[AspNetRoles] r ON r.[Id] = ur.[RoleId]
WHERE r.[NormalizedName] = N'ADMIN';

IF @Loi IS NOT NULL
BEGIN
    SET @Loi = CONCAT(N'Danh sach co tai khoan Admin (', @Loi, N'). Script nay chi danh cho tai khoan test. Khong co gi bi thay doi.');
    THROW 51031, @Loi, 1;
END;

DECLARE @SoDong int;
BEGIN TRANSACTION;
    UPDATE u SET [IsActive] = 0
    FROM [identity].[AspNetUsers] u
    JOIN @TaiKhoan t ON u.[NormalizedUserName] = UPPER(t.UserName) COLLATE DATABASE_DEFAULT
    WHERE u.[IsActive] = 1;
    SET @SoDong = @@ROWCOUNT;
COMMIT TRANSACTION;

DECLARE @TrangThai nvarchar(2000) = (
    SELECT STRING_AGG(CONVERT(nvarchar(max), CONCAT(t.UserName, N' IsActive=',
        COALESCE(CONVERT(nvarchar(1), u.[IsActive]), N'(khong ton tai)'))), N', ')
    FROM @TaiKhoan t
    LEFT JOIN [identity].[AspNetUsers] u ON u.[NormalizedUserName] = UPPER(t.UserName) COLLATE DATABASE_DEFAULT);

PRINT CONCAT(N'Da vo hieu hoa ', @SoDong, N' tai khoan. Hien tai: ', @TrangThai);
PRINT N'Dang nhap thu mot lan voi tai khoan test, ket qua phai la 401. Ghi so tien do: thoi diem vo hieu hoa.';
GO
