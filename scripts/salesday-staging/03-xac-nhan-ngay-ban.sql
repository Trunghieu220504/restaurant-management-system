/*
    03-xac-nhan-ngay-ban.sql
    Nghiem thu PHU-1.2 / DUY-1.3 tren staging - phan viec cua Phuong.

    CHI DOC du lieu. Xac nhan ban ghi salesday.NgayBan sau khi mo ngay tren giao dien:
    dung ngay, so tien, nguoi mo, trang thai DANG_BAN; chi mot ngay duoc tao;
    tai khoan khong co quyen khong tao duoc ngay; tai lai trang khong lam doi du lieu.

    Lan 1 (sau ca 1-2): dien @NgayKinhDoanh, @TienDauNgay, @SoNgayTruocTest.
                        Ghi lai Id va Version trong dong "BAN GHI".
    Lan 2 (sau ca 3, tai lai trang): dien them @IdDaGhiNhan, @VersionDaGhiNhan.
    Lan 3 (sau ca 4, nguoi thu hai bam mo): giu nguyen tham so, chay lai.
    Tham so de NULL thi script chi in gia tri de doi chieu bang mat.

    Neu co [KHONG DAT], script ket thuc bang loi 50003 (sqlcmd -b tra ma khac 0).
    Cach chay: docs/nghiem-thu-salesday-staging.md
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

-- ===== THAM SO =====
DECLARE @NgayKinhDoanh date = NULL;             -- ngay nhom thong nhat, vi du '2026-11-19'
DECLARE @TienDauNgay bigint = NULL;             -- VND, so nguyen, vi du 1250000
DECLARE @SoNgayTruocTest int = NULL;            -- so o dong C1 cua 01 (chay truoc buoi test)
DECLARE @IdDaGhiNhan uniqueidentifier = NULL;   -- ca 3: Id in ra o lan chay truoc
DECLARE @VersionDaGhiNhan varchar(18) = NULL;   -- ca 3: Version in ra o lan chay truoc, dang 0x...
DECLARE @DuocMoNgay TABLE (UserName nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
INSERT @DuocMoNgay (UserName) VALUES (N'manager.test');   -- them dong neu ca 4 dung 2 tai khoan Manager
DECLARE @KhongCoQuyen TABLE (UserName nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
INSERT @KhongCoQuyen (UserName) VALUES (N'staff.test');   -- tai khoan dung o ca 5
-- ===================

DECLARE @KQ TABLE (
    STT int IDENTITY(1, 1) PRIMARY KEY,
    Ma varchar(4) NOT NULL,
    KetQua varchar(9) NOT NULL,
    NoiDung nvarchar(3000) NOT NULL);

DECLARE @GioVN datetime2(0) = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+07:00') AS datetime2(0));
DECLARE @Tam nvarchar(max);

INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('INFO', 'THONG TIN',
    CONCAT(N'Database ', DB_NAME(), N' | chay luc ', CONVERT(nvarchar(19), @GioVN, 120), N' gio VN'));

DECLARE @TongSoNgay int, @SoHoatDong int;
SELECT @TongSoNgay = COUNT(*),
    @SoHoatDong = COALESCE(SUM(CASE WHEN [ActiveSlot] IS NOT NULL THEN 1 ELSE 0 END), 0)
FROM [salesday].[NgayBan];

DECLARE @Id uniqueidentifier, @Ngay date, @Tien bigint, @TrangThai nvarchar(16), @NguoiMoId uniqueidentifier,
    @MoLuc datetimeoffset(7), @RequestKey uniqueidentifier, @Version binary(8);
SELECT TOP (1) @Id = d.[Id], @Ngay = d.[BusinessDate], @Tien = d.[OpeningCash], @TrangThai = d.[Status],
    @NguoiMoId = d.[OpenedBy], @MoLuc = d.[OpenedAt], @RequestKey = d.[RequestKey], @Version = d.[Version]
FROM [salesday].[NgayBan] d
WHERE d.[ActiveSlot] IS NOT NULL
ORDER BY d.[OpenedAt] DESC;

-- N1. Dung mot ngay hoat dong (ca 1)
IF @SoHoatDong = 1
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('N1', 'DAT', N'Co dung 1 ngay ban hoat dong (ActiveSlot = 1).');
ELSE IF @SoHoatDong = 0
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('N1', 'KHONG DAT',
        N'Chua co ngay ban hoat dong: ngay chua duoc mo, hoac API dang dung database khac.');
ELSE
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('N1', 'KHONG DAT',
        CONCAT(N'Co ', @SoHoatDong, N' ngay hoat dong: vi pham "chi mot ngay hoat dong", kiem tra IX_NgayBan_ActiveSlot.'));

IF @Id IS NOT NULL
BEGIN
    DECLARE @NguoiMo nvarchar(256) = (SELECT u.[UserName] FROM [identity].[AspNetUsers] u WHERE u.[Id] = @NguoiMoId);
    DECLARE @NguoiMoCoQuyen bit = CASE WHEN EXISTS (
        SELECT 1 FROM [identity].[AspNetUserRoles] ur
        JOIN [identity].[RolePermission] rp ON rp.[RoleId] = ur.[RoleId]
        WHERE ur.[UserId] = @NguoiMoId AND rp.[PermissionCode] = 'salesday.manage') THEN 1 ELSE 0 END;
    DECLARE @MoLucVN datetime2(0) = CAST(SWITCHOFFSET(@MoLuc, '+07:00') AS datetime2(0));
    DECLARE @TienChuoi varchar(40) = CASE WHEN @Tien BETWEEN 0 AND 900000000000000
        THEN REPLACE(LEFT(CONVERT(varchar(40), CAST(@Tien AS money), 1), LEN(CONVERT(varchar(40), CAST(@Tien AS money), 1)) - 3), ',', '.')
        ELSE CONVERT(varchar(40), @Tien) END;
    DECLARE @VersionChuoi varchar(18) = CONVERT(varchar(18), @Version, 1);

    -- Dong de chep vao bien ban; dinh dang giong giao dien /sales-day
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('GHI', 'THONG TIN',
        CONCAT(N'BAN GHI: Id ', @Id, N' | ngay ', CONVERT(varchar(10), @Ngay, 103), N' | tien dau ngay ', @TienChuoi,
            N' VND | ', @TrangThai, N' | nguoi mo ', COALESCE(@NguoiMo, CONCAT(N'(khong tim thay user ', @NguoiMoId, N')')),
            N' | mo luc ', CONVERT(varchar(10), @MoLucVN, 103), N' ', CONVERT(varchar(8), @MoLucVN, 108),
            N' gio VN (luu ', CAST(@MoLuc AS varchar(34)), N') | Version ', @VersionChuoi, N' | RequestKey ', @RequestKey));

    -- N2. Trang thai (ca 2: giao dien hien DANG BAN)
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'N2', CASE WHEN @TrangThai = N'DANG_BAN' THEN 'DAT' ELSE 'KHONG DAT' END,
        CONCAT(N'Trang thai luu: ', @TrangThai, N' (mong doi DANG_BAN, giao dien hien "DANG BAN").');

    -- N3. Ngay kinh doanh (ca 2)
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'N3',
        CASE WHEN @NgayKinhDoanh IS NULL THEN 'THONG TIN' WHEN @NgayKinhDoanh = @Ngay THEN 'DAT' ELSE 'KHONG DAT' END,
        CONCAT(N'Ngay luu: ', CONVERT(varchar(10), @Ngay, 23),
            CASE WHEN @NgayKinhDoanh IS NULL THEN N' (chua dien @NgayKinhDoanh, doi chieu bang mat)'
                ELSE CONCAT(N'; nhom thong nhat: ', CONVERT(varchar(10), @NgayKinhDoanh, 23)) END);

    -- N4. Tien dau ngay (ca 2)
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'N4',
        CASE WHEN @TienDauNgay IS NULL THEN 'THONG TIN' WHEN @TienDauNgay = @Tien THEN 'DAT' ELSE 'KHONG DAT' END,
        CONCAT(N'Tien dau ngay luu: ', @Tien,
            CASE WHEN @TienDauNgay IS NULL THEN N' (chua dien @TienDauNgay, doi chieu bang mat)'
                ELSE CONCAT(N'; nhom thong nhat: ', @TienDauNgay) END);

    -- N5. Nguoi mo dung tai khoan duoc giao (ca 2)
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'N5',
        CASE WHEN @NguoiMo IS NULL THEN 'KHONG DAT'
            WHEN NOT EXISTS (SELECT 1 FROM @DuocMoNgay) THEN 'THONG TIN'
            WHEN EXISTS (SELECT 1 FROM @DuocMoNgay x
                WHERE UPPER(x.UserName) COLLATE DATABASE_DEFAULT = UPPER(@NguoiMo) COLLATE DATABASE_DEFAULT) THEN 'DAT'
            ELSE 'KHONG DAT' END,
        CONCAT(N'Nguoi mo (OpenedBy): ', COALESCE(@NguoiMo, CONCAT(N'khong tim thay user ', @NguoiMoId)),
            N'; duoc giao: ', COALESCE((SELECT STRING_AGG(UserName, N', ') FROM @DuocMoNgay), N'(khong dien)'));

    -- N6. Nguoi mo co quyen salesday.manage (backend kiem tra quyen)
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'N6', CASE WHEN @NguoiMoCoQuyen = 1 THEN 'DAT' ELSE 'KHONG DAT' END,
        CASE WHEN @NguoiMoCoQuyen = 1 THEN N'Nguoi mo co quyen salesday.manage qua vai tro.'
            ELSE N'Nguoi mo KHONG co quyen salesday.manage: nghi loi phan quyen, bao Hieu/Phuong.' END;

    -- N7. Thoi diem mo nam trong buoi test
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'N7',
        CASE WHEN @MoLuc BETWEEN DATEADD(hour, -24, SYSDATETIMEOFFSET()) AND DATEADD(minute, 5, SYSDATETIMEOFFSET())
            THEN 'DAT' ELSE 'CANH BAO' END,
        CONCAT(N'Mo luc ', CONVERT(nvarchar(19), @MoLucVN, 120), N' gio VN',
            CASE WHEN @MoLuc < DATEADD(hour, -24, SYSDATETIMEOFFSET())
                THEN N'; ngay nay mo truoc 24 gio qua, co the khong phai do buoi test tao' ELSE N'' END);

    -- N8. Khoa chong gui lap (Idempotency-Key)
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'N8', CASE WHEN @RequestKey <> '00000000-0000-0000-0000-000000000000' THEN 'DAT' ELSE 'KHONG DAT' END,
        CONCAT(N'RequestKey ', @RequestKey, N' (trung header Idempotency-Key cua POST /api/salesday/open neu co log DevTools).');

    -- N9. Ca 3: tai lai trang khong lam doi ban ghi
    IF @IdDaGhiNhan IS NULL AND @VersionDaGhiNhan IS NULL
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('N9', 'THONG TIN',
            CONCAT(N'Lan chay dau: ghi lai Id ', @Id, N' va Version ', @VersionChuoi,
                N' de dien @IdDaGhiNhan/@VersionDaGhiNhan sau khi tai lai trang (ca 3).'));
    ELSE
        INSERT @KQ (Ma, KetQua, NoiDung)
        SELECT 'N9',
            CASE WHEN (@IdDaGhiNhan IS NULL OR @IdDaGhiNhan = @Id)
                    AND (@VersionDaGhiNhan IS NULL OR UPPER(LTRIM(RTRIM(@VersionDaGhiNhan))) = UPPER(@VersionChuoi))
                THEN 'DAT' ELSE 'KHONG DAT' END,
            CONCAT(N'Id hien tai ', @Id, N' (lan truoc ', COALESCE(CONVERT(nvarchar(36), @IdDaGhiNhan), N'-'),
                N'), Version hien tai ', @VersionChuoi, N' (lan truoc ', COALESCE(@VersionDaGhiNhan, N'-'),
                N'). Giong nhau = du lieu khong mat, khong bi ghi lai.');

    -- N10. Ca 4: ngay kinh doanh nay chi co mot ban ghi
    SELECT @Tam = CONVERT(nvarchar(20), COUNT(*)) FROM [salesday].[NgayBan] WHERE [BusinessDate] = @Ngay;
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'N10', CASE WHEN @Tam = N'1' THEN 'DAT' ELSE 'KHONG DAT' END,
        CONCAT(N'So ban ghi cua ngay ', CONVERT(varchar(10), @Ngay, 23), N': ', @Tam, N' (mong doi 1).');
END;

-- N11. Ca 4: ca buoi test chi tao them dung mot ngay
INSERT @KQ (Ma, KetQua, NoiDung)
SELECT 'N11',
    CASE WHEN @SoNgayTruocTest IS NULL THEN 'THONG TIN' WHEN @TongSoNgay = @SoNgayTruocTest + 1 THEN 'DAT' ELSE 'KHONG DAT' END,
    CONCAT(N'Tong so ngay ban hien co: ', @TongSoNgay,
        CASE WHEN @SoNgayTruocTest IS NULL THEN N' (chua dien @SoNgayTruocTest tu dong C1 cua 01)'
            ELSE CONCAT(N'; truoc buoi test: ', @SoNgayTruocTest, N'; mong doi ', @SoNgayTruocTest + 1) END);

-- N12. Ca 5: tai khoan khong co quyen khong tao duoc ngay nao
SET @Tam = NULL;
SELECT @Tam = STRING_AGG(CONVERT(nvarchar(max), CONCAT(u.[UserName], N' mo ngay ', CONVERT(varchar(10), d.[BusinessDate], 23))), N'; ')
FROM [salesday].[NgayBan] d
JOIN [identity].[AspNetUsers] u ON u.[Id] = d.[OpenedBy]
WHERE EXISTS (SELECT 1 FROM @KhongCoQuyen k
    WHERE UPPER(k.UserName) COLLATE DATABASE_DEFAULT = u.[NormalizedUserName] COLLATE DATABASE_DEFAULT);
INSERT @KQ (Ma, KetQua, NoiDung)
SELECT 'N12', CASE WHEN @Tam IS NULL THEN 'DAT' ELSE 'KHONG DAT' END,
    CASE WHEN @Tam IS NULL
        THEN CONCAT(N'Khong co ngay nao do ', COALESCE((SELECT STRING_AGG(UserName, N', ') FROM @KhongCoQuyen), N'(khong dien)'), N' mo.')
        ELSE CONCAT(N'Tai khoan khong co quyen da mo duoc ngay: ', @Tam) END;

-- N13. Moi ngay ban deu do nguoi co quyen salesday.manage mo
SET @Tam = NULL;
SELECT @Tam = STRING_AGG(CONVERT(nvarchar(max), CONCAT(COALESCE(u.[UserName], CONVERT(nvarchar(36), d.[OpenedBy])),
        N' mo ngay ', CONVERT(varchar(10), d.[BusinessDate], 23))), N'; ')
FROM [salesday].[NgayBan] d
LEFT JOIN [identity].[AspNetUsers] u ON u.[Id] = d.[OpenedBy]
WHERE NOT EXISTS (
    SELECT 1 FROM [identity].[AspNetUserRoles] ur
    JOIN [identity].[RolePermission] rp ON rp.[RoleId] = ur.[RoleId]
    WHERE ur.[UserId] = d.[OpenedBy] AND rp.[PermissionCode] = 'salesday.manage');
INSERT @KQ (Ma, KetQua, NoiDung)
SELECT 'N13', CASE WHEN @Tam IS NULL THEN 'DAT' ELSE 'CANH BAO' END,
    CASE WHEN @Tam IS NULL THEN N'Moi ngay ban hien co deu do tai khoan co quyen salesday.manage mo.'
        ELSE CONCAT(N'Ngay do tai khoan hien khong co quyen (hoac khong ton tai) mo, can giai thich: ', @Tam) END;

/* ---------- Tong ket va in ket qua ---------- */

INSERT @KQ (Ma, KetQua, NoiDung)
SELECT 'TONG',
    CASE WHEN EXISTS (SELECT 1 FROM @KQ WHERE KetQua = 'KHONG DAT') THEN 'KHONG DAT' ELSE 'DAT' END,
    CASE WHEN EXISTS (SELECT 1 FROM @KQ WHERE KetQua = 'KHONG DAT')
        THEN N'Ban ghi ngay ban CHUA dung: xem cac dong KHONG DAT o tren.'
        ELSE N'Ban ghi ngay ban luu dung voi cac tham so da dien (dong THONG TIN can doi chieu bang mat).' END;

DECLARE @i int = 1, @SoDong int = (SELECT MAX(STT) FROM @KQ), @Dong nvarchar(4000);
PRINT N'=== 03-xac-nhan-ngay-ban.sql ===';
WHILE @i <= @SoDong
BEGIN
    SELECT @Dong = CONCAT(N'[', LEFT(KetQua + '         ', 9), N'] ', Ma, N' ', NoiDung) FROM @KQ WHERE STT = @i;
    PRINT @Dong;
    SET @i += 1;
END;

IF EXISTS (SELECT 1 FROM @KQ WHERE KetQua = 'KHONG DAT')
    THROW 50003, N'Co muc KHONG DAT. Ghi vao bien ban va bao nguoi phu trach.', 1;
GO
