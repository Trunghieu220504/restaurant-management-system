/*
    04-hoan-tac-ngay-test.sql
    Nghiem thu PHU-1.2 / DUY-1.3 tren staging - CHI DUNG KHI PHAI LAM LAI CA NGHIEM THU.

    GHI DU LIEU: xoa MOT dong salesday.NgayBan do buoi test tao ra.
    Chua co API chot ngay, nen sau khi mo ngay thi khong mo lai duoc; script nay
    dua database ve trang thai truoc buoi test. KHONG dung de chot hoac xoa ngay ban that.

    Chi chay khi:
      - Hai va Hieu da dong y (ghi vao so tien do: ai dong y, luc nao, ly do);
      - da chay 03 va chep dong "BAN GHI" vao bien ban.

    Script tu chan neu:
      - @XacNhan khong dung cau quy dinh, hoac sai database;
      - Id khong ton tai, ngay khong do tai khoan test mo, hoac mo qua 72 gio;
      - da co du lieu module khac tham chieu ngay nay (cot NgayBanId kieu uniqueidentifier).
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
DECLARE @IdNgay uniqueidentifier = NULL;                    -- Id trong dong "BAN GHI" cua 03
DECLARE @XacNhan nvarchar(100) = N'';                       -- go dung: HAI VA HIEU DA DONG Y
DECLARE @TaiKhoanTest TABLE (UserName nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
INSERT @TaiKhoanTest (UserName) VALUES (N'manager.test'), (N'staff.test');
-- ===================

DECLARE @Loi nvarchar(2000);

IF @XacNhan <> N'HAI VA HIEU DA DONG Y'
    THROW 51020, N'Chua xac nhan: dien @XacNhan = N''HAI VA HIEU DA DONG Y'' sau khi nhom dong y. Khong co gi bi thay doi.', 1;

IF DB_NAME() <> @DatabaseDich
BEGIN
    SET @Loi = CONCAT(N'Dang o database ', DB_NAME(), N', khong phai ', @DatabaseDich, N'. Khong co gi bi thay doi.');
    THROW 51021, @Loi, 1;
END;

IF @IdNgay IS NULL
    THROW 51022, N'Chua dien @IdNgay (lay tu dong BAN GHI cua 03). Khong co gi bi thay doi.', 1;

DECLARE @Ngay date, @Tien bigint, @TrangThai nvarchar(16), @NguoiMo nvarchar(256), @NguoiMoId uniqueidentifier,
    @MoLuc datetimeoffset(7), @RequestKey uniqueidentifier, @TimThay int;

SELECT @Ngay = d.[BusinessDate], @Tien = d.[OpeningCash], @TrangThai = d.[Status], @NguoiMoId = d.[OpenedBy],
    @MoLuc = d.[OpenedAt], @RequestKey = d.[RequestKey], @NguoiMo = u.[UserName]
FROM [salesday].[NgayBan] d
LEFT JOIN [identity].[AspNetUsers] u ON u.[Id] = d.[OpenedBy]
WHERE d.[Id] = @IdNgay;
SET @TimThay = @@ROWCOUNT;

IF @TimThay = 0
    THROW 51023, N'Khong tim thay ngay ban voi Id da dien. Khong co gi bi thay doi.', 1;

IF @NguoiMo IS NULL OR NOT EXISTS (SELECT 1 FROM @TaiKhoanTest t
    WHERE UPPER(t.UserName) COLLATE DATABASE_DEFAULT = UPPER(@NguoiMo) COLLATE DATABASE_DEFAULT)
BEGIN
    SET @Loi = CONCAT(N'Ngay nay do ', COALESCE(@NguoiMo, CONVERT(nvarchar(36), @NguoiMoId)),
        N' mo, khong phai tai khoan test. Khong xoa.');
    THROW 51024, @Loi, 1;
END;

IF @MoLuc < DATEADD(hour, -72, SYSDATETIMEOFFSET())
    THROW 51025, N'Ngay nay mo qua 72 gio truoc, khong coi la du lieu cua buoi test vua roi. Khong xoa.', 1;

PRINT CONCAT(N'Se xoa ngay ban: Id ', @IdNgay, N' | ngay ', CONVERT(varchar(10), @Ngay, 23), N' | tien ', @Tien,
    N' | ', @TrangThai, N' | nguoi mo ', @NguoiMo, N' | mo luc ', CAST(@MoLuc AS varchar(34)), N' | RequestKey ', @RequestKey);

DROP TABLE IF EXISTS #ThamChieu;
CREATE TABLE #ThamChieu (Bang nvarchar(300) COLLATE DATABASE_DEFAULT NOT NULL, SoDong bigint NOT NULL);
DECLARE @Sql nvarchar(max), @KetQuaKhoa int, @DaXoa int;

BEGIN TRANSACTION;
    -- Cung khoa voi API mo ngay (BusinessDays.OpenAsync): khong ai mo ngay xen vao luc dang hoan tac.
    -- Module khac chua co khoa ngay (ISalesDayGate moi la hop dong), nen chi chay khi khong ai dang test tren staging.
    EXEC @KetQuaKhoa = sys.sp_getapplock @Resource = N'restaurant:salesday:open', @LockMode = 'Exclusive',
        @LockOwner = 'Transaction', @LockTimeout = 10000;
    IF @KetQuaKhoa < 0
        THROW 51028, N'Khong lay duoc khoa mo ngay sau 10 giay. Khong co gi bi thay doi.', 1;

    -- Du lieu module khac tham chieu ngay nay (catalog.Het*, sales.DonHang...): khong co khoa ngoai xuyen schema nen tu kiem tra
    SELECT @Sql = STRING_AGG(CONVERT(nvarchar(max), CONCAT(
            N'INSERT #ThamChieu (Bang, SoDong) SELECT N''', REPLACE(s.name, N'''', N''''''), N'.', REPLACE(t.name, N'''', N''''''),
            N''', COUNT_BIG(*) FROM ', QUOTENAME(s.name), N'.', QUOTENAME(t.name), N' WHERE ', QUOTENAME(c.name), N' = @IdNgay;')),
        NCHAR(10))
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE c.name = N'NgayBanId' AND TYPE_NAME(c.user_type_id) = N'uniqueidentifier';

    IF @Sql IS NOT NULL
        EXEC sys.sp_executesql @Sql, N'@IdNgay uniqueidentifier', @IdNgay = @IdNgay;

    SELECT @Loi = STRING_AGG(CONVERT(nvarchar(max), CONCAT(Bang, N'=', SoDong)), N', ') FROM #ThamChieu WHERE SoDong > 0;
    IF @Loi IS NOT NULL
    BEGIN
        SET @Loi = CONCAT(N'Da co du lieu gan voi ngay nay: ', @Loi, N'. Khong xoa; bao Hai/Hieu.');
        THROW 51026, @Loi, 1;
    END;

    DELETE FROM [salesday].[NgayBan] WHERE [Id] = @IdNgay AND [OpenedBy] = @NguoiMoId;
    SET @DaXoa = @@ROWCOUNT;
    IF @DaXoa <> 1
        THROW 51027, N'So dong bi xoa khac 1. Da huy, khong co gi bi thay doi.', 1;
COMMIT TRANSACTION;

DECLARE @ConHoatDong int = (SELECT COUNT(*) FROM [salesday].[NgayBan] WHERE [ActiveSlot] IS NOT NULL);
PRINT CONCAT(N'Da xoa 1 ngay ban. So ngay hoat dong con lai: ', @ConHoatDong,
    N'. Ghi vao so tien do: nguoi chay, thoi diem, ly do, nguoi dong y. Chay lai 01 truoc khi test lai.');

DROP TABLE #ThamChieu;
GO
