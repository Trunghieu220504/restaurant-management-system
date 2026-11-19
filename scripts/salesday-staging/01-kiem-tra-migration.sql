/*
    01-kiem-tra-migration.sql
    Nghiem thu PHU-1.2 / DUY-1.3 tren staging - phan viec cua Phuong.

    CHI DOC du lieu: khong INSERT/UPDATE/DELETE bang nao cua ung dung
    (chi dung bang tam #... trong tempdb). Chay truoc buoi nghiem thu va
    co the chay lai bat ky luc nao.

    Kiem tra:
      A. Migration 20261110074313_InitialSalesDay da ap dung; cau truc
         salesday.NgayBan (cot, khoa chinh, CHECK, 3 chi muc unique) khop migration.
      B. Identity: migration, vai tro, quyen salesday.manage, tai khoan test.
      C. Trang thai ngay ban hien tai: dieu kien de mo ngay tren giao dien.

    Moi dong ket qua bat dau bang [DAT], [KHONG DAT], [CANH BAO] hoac [THONG TIN].
    Neu co [KHONG DAT], script ket thuc bang loi 50001 (sqlcmd -b tra ma khac 0).
    Cach chay va cach doc ket qua: docs/nghiem-thu-salesday-staging.md
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

-- ===== THAM SO: sua neu nhom dung ten tai khoan test khac =====
DECLARE @TaiKhoanTest TABLE (
    UserName nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
    VaiTro nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL,
    CoQuyenMoNgay bit NOT NULL);
INSERT @TaiKhoanTest (UserName, VaiTro, CoQuyenMoNgay) VALUES
    (N'manager.test', N'Manager', 1),
    (N'staff.test', N'Staff', 0);
-- ==============================================================

DECLARE @KQ TABLE (
    STT int IDENTITY(1, 1) PRIMARY KEY,
    Ma varchar(4) NOT NULL,
    KetQua varchar(9) NOT NULL,
    NoiDung nvarchar(3000) NOT NULL);

DECLARE @GioVN datetime2(0) = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+07:00') AS datetime2(0));
DECLARE @HomNayVN date = CAST(@GioVN AS date);
DECLARE @Bang int = OBJECT_ID(N'[salesday].[NgayBan]', N'U');
DECLARE @CauTrucDung bit = 0;
DECLARE @Tam nvarchar(max);
DECLARE @Sql nvarchar(max);

INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('INFO', 'THONG TIN',
    CONCAT(N'Database ', DB_NAME(), N' | SQL Server ', CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')),
        N' | chay luc ', CONVERT(nvarchar(19), @GioVN, 120), N' gio VN'));

/* ---------- A. Migration va cau truc SalesDay ---------- */

-- A1. Lich su migration cua module SalesDay
IF OBJECT_ID(N'[salesday].[__EFMigrationsHistory]', N'U') IS NULL
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('A1', 'KHONG DAT',
        N'Khong co bang salesday.__EFMigrationsHistory: migration SalesDay chua duoc ap dung tren database nay.');
ELSE
BEGIN
    SELECT @Tam = STRING_AGG(CONVERT(nvarchar(max), CONCAT(MigrationId, N' (EF ', ProductVersion, N')')), N', ')
        WITHIN GROUP (ORDER BY MigrationId)
    FROM [salesday].[__EFMigrationsHistory];

    IF NOT EXISTS (SELECT 1 FROM [salesday].[__EFMigrationsHistory] WHERE MigrationId = N'20261110074313_InitialSalesDay')
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('A1', 'KHONG DAT',
            CONCAT(N'Lich su migration SalesDay khong co 20261110074313_InitialSalesDay. Hien co: ', COALESCE(@Tam, N'(trong)')));
    ELSE IF (SELECT COUNT(*) FROM [salesday].[__EFMigrationsHistory]) > 1
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('A1', 'CANH BAO',
            CONCAT(N'Co InitialSalesDay nhung con migration khac chua co trong develop df6160c, can doi chieu ma nguon: ', @Tam));
    ELSE
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('A1', 'DAT', CONCAT(N'Lich su migration SalesDay: ', @Tam));
END;

-- A2. Bang salesday.NgayBan
IF @Bang IS NULL
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('A2', 'KHONG DAT', N'Khong co bang salesday.NgayBan.');
ELSE
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('A2', 'DAT', N'Co bang salesday.NgayBan.');

IF @Bang IS NOT NULL
BEGIN
    -- A3. Cot: dung ten, kieu, do dai, NULL/NOT NULL; khong thieu, khong thua
    DECLARE @CotMongDoi TABLE (
        Ten sysname COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
        Kieu sysname COLLATE DATABASE_DEFAULT NOT NULL,
        DoDaiByte smallint NULL,
        ChoPhepNull bit NOT NULL);
    INSERT @CotMongDoi (Ten, Kieu, DoDaiByte, ChoPhepNull) VALUES
        (N'Id', N'uniqueidentifier', NULL, 0),
        (N'BusinessDate', N'date', NULL, 0),
        (N'OpenedAt', N'datetimeoffset', NULL, 0),
        (N'OpenedBy', N'uniqueidentifier', NULL, 0),
        (N'OpeningCash', N'bigint', NULL, 0),
        (N'Status', N'nvarchar', 32, 0),          -- nvarchar(16) = 32 byte
        (N'ActiveSlot', N'int', NULL, 1),
        (N'RequestKey', N'uniqueidentifier', NULL, 0),
        (N'Version', N'timestamp', NULL, 0);      -- rowversion

    SELECT @Tam = STRING_AGG(CONVERT(nvarchar(max), CONCAT(COALESCE(m.Ten, a.Ten),
            N' (mong doi ', CASE WHEN m.Ten IS NULL THEN N'khong co'
                ELSE CONCAT(m.Kieu, CASE WHEN m.DoDaiByte IS NULL THEN N'' ELSE CONCAT(N'/', m.DoDaiByte, N'B') END,
                    CASE m.ChoPhepNull WHEN 1 THEN N' NULL' ELSE N' NOT NULL' END) END,
            N'; thuc te ', CASE WHEN a.Ten IS NULL THEN N'khong co'
                ELSE CONCAT(a.Kieu, N'/', a.DoDaiByte, N'B', CASE a.ChoPhepNull WHEN 1 THEN N' NULL' ELSE N' NOT NULL' END) END,
            N')')), N'; ')
    FROM @CotMongDoi m
    FULL OUTER JOIN (
        SELECT c.name AS Ten, TYPE_NAME(c.user_type_id) AS Kieu, c.max_length AS DoDaiByte, c.is_nullable AS ChoPhepNull
        FROM sys.columns c
        WHERE c.object_id = @Bang) a
        ON a.Ten COLLATE DATABASE_DEFAULT = m.Ten COLLATE DATABASE_DEFAULT
    WHERE m.Ten IS NULL OR a.Ten IS NULL
        OR a.Kieu COLLATE DATABASE_DEFAULT <> m.Kieu COLLATE DATABASE_DEFAULT
        OR a.ChoPhepNull <> m.ChoPhepNull
        OR (m.DoDaiByte IS NOT NULL AND a.DoDaiByte <> m.DoDaiByte);

    IF @Tam IS NULL
    BEGIN
        SET @CauTrucDung = 1;
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('A3', 'DAT',
            N'9 cot khop migration: Id, BusinessDate, OpenedAt, OpenedBy, OpeningCash, Status nvarchar(16), ActiveSlot NULL, RequestKey, Version rowversion.');
    END
    ELSE
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('A3', 'KHONG DAT', CONCAT(N'Cot lech migration: ', @Tam));

    -- A4. Khoa chinh PK_NgayBan tren Id
    SELECT @Tam = STRING_AGG(col.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
    FROM sys.key_constraints k
    JOIN sys.index_columns ic ON ic.object_id = k.parent_object_id AND ic.index_id = k.unique_index_id
    JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
    WHERE k.parent_object_id = @Bang AND k.type = 'PK' AND k.name = N'PK_NgayBan';

    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'A4', CASE WHEN @Tam = N'Id' THEN 'DAT' ELSE 'KHONG DAT' END,
        CONCAT(N'Khoa chinh PK_NgayBan: ', COALESCE(@Tam, N'khong ton tai'));

    -- A5. Hai rang buoc CHECK: dang bat, duoc tin cay, noi dung dung
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'A5',
        CASE WHEN cc.object_id IS NOT NULL AND cc.is_disabled = 0 AND cc.is_not_trusted = 0
                AND ((m.Ten = N'CK_NgayBan_Cash' AND x.ChuanHoa = N'OPENINGCASH>=0')
                    OR (m.Ten = N'CK_NgayBan_State'
                        AND CHARINDEX(N'''DANG_BAN''', x.ChuanHoa) > 0
                        AND CHARINDEX(N'''CHO_CHOT''', x.ChuanHoa) > 0
                        AND CHARINDEX(N'''DA_CHOT''', x.ChuanHoa) > 0
                        AND CHARINDEX(N'ACTIVESLOT=1', x.ChuanHoa) > 0
                        AND CHARINDEX(N'ACTIVESLOTISNOTNULL', x.ChuanHoa) > 0
                        AND CHARINDEX(N'ACTIVESLOTISNULL', x.ChuanHoa) > 0))
            THEN 'DAT' ELSE 'KHONG DAT' END,
        CONCAT(m.Ten, N': ', CASE WHEN cc.object_id IS NULL THEN N'khong ton tai'
            ELSE CONCAT(cc.[definition], N' (disabled=', cc.is_disabled, N', not_trusted=', cc.is_not_trusted, N')') END)
    FROM (VALUES (N'CK_NgayBan_Cash'), (N'CK_NgayBan_State')) m(Ten)
    LEFT JOIN sys.check_constraints cc
        ON cc.parent_object_id = @Bang AND cc.name COLLATE DATABASE_DEFAULT = m.Ten COLLATE DATABASE_DEFAULT
    OUTER APPLY (SELECT UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(cc.[definition],
        N'[', N''), N']', N''), N'(', N''), N')', N''), N' ', N'')) AS ChuanHoa) x
    ORDER BY m.Ten;

    -- A6. Ba chi muc unique; IX_NgayBan_ActiveSlot co bo loc => toi da 1 ngay hoat dong o muc database
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'A6',
        CASE WHEN i.index_id IS NOT NULL AND i.is_unique = 1 AND i.is_disabled = 0
                AND k.Cot COLLATE DATABASE_DEFAULT = m.Cot COLLATE DATABASE_DEFAULT
                AND i.has_filter = m.CoLoc
                AND (m.CoLoc = 0 OR UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(i.filter_definition,
                    N'[', N''), N']', N''), N'(', N''), N')', N''), N' ', N'')) = N'ACTIVESLOTISNOTNULL')
            THEN 'DAT' ELSE 'KHONG DAT' END,
        CONCAT(m.Ten, N': ', CASE WHEN i.index_id IS NULL THEN N'khong ton tai'
            ELSE CONCAT(N'unique=', i.is_unique, N', cot=', k.Cot, N', loc=', COALESCE(i.filter_definition, N'(khong)'),
                N', disabled=', i.is_disabled) END)
    FROM (VALUES
            (N'IX_NgayBan_ActiveSlot', N'ActiveSlot', CAST(1 AS bit)),
            (N'IX_NgayBan_BusinessDate', N'BusinessDate', CAST(0 AS bit)),
            (N'IX_NgayBan_RequestKey', N'RequestKey', CAST(0 AS bit))) m(Ten, Cot, CoLoc)
    LEFT JOIN sys.indexes i
        ON i.object_id = @Bang AND i.name COLLATE DATABASE_DEFAULT = m.Ten COLLATE DATABASE_DEFAULT
    OUTER APPLY (
        SELECT STRING_AGG(c.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Cot
        FROM sys.index_columns ic
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0) k
    ORDER BY m.Ten;
END;

/* ---------- B. Identity: dang nhap, vai tro, quyen, tai khoan test ---------- */

-- B1. Migration Identity can cho dang nhap/refresh tren ban dang deploy
IF OBJECT_ID(N'[identity].[__EFMigrationsHistory]', N'U') IS NULL
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('B1', 'KHONG DAT', N'Khong co bang identity.__EFMigrationsHistory.');
ELSE
BEGIN
    SELECT @Tam = STRING_AGG(m.MigrationId, N', ')
    FROM (VALUES
            (N'20261105063121_InitialIdentity'),
            (N'20261105073827_IdentityLoginSessions'),
            (N'20261110175236_IdentityRefreshTokens')) m(MigrationId)
    WHERE NOT EXISTS (
        SELECT 1 FROM [identity].[__EFMigrationsHistory] h
        WHERE h.MigrationId COLLATE DATABASE_DEFAULT = m.MigrationId COLLATE DATABASE_DEFAULT);

    IF @Tam IS NULL
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('B1', 'DAT',
            N'Identity co du InitialIdentity, IdentityLoginSessions, IdentityRefreshTokens.');
    ELSE
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('B1', 'KHONG DAT', CONCAT(N'Identity thieu migration: ', @Tam));
END;

IF OBJECT_ID(N'[identity].[AspNetRoles]', N'U') IS NOT NULL
    AND OBJECT_ID(N'[identity].[RolePermission]', N'U') IS NOT NULL
    AND OBJECT_ID(N'[identity].[Permission]', N'U') IS NOT NULL
    AND OBJECT_ID(N'[identity].[AspNetUsers]', N'U') IS NOT NULL
    AND OBJECT_ID(N'[identity].[AspNetUserRoles]', N'U') IS NOT NULL
BEGIN
    -- B2. Vai tro Manager va Staff (ID co dinh do migration seed)
    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'B2',
        CASE WHEN r.Id IS NULL THEN 'KHONG DAT' WHEN r.Id <> v.Id THEN 'CANH BAO' ELSE 'DAT' END,
        CASE WHEN r.Id IS NULL THEN CONCAT(N'Khong co vai tro ', v.Ten)
            ELSE CONCAT(N'Vai tro ', r.Name, N' id ', CONVERT(nvarchar(36), r.Id),
                CASE WHEN r.Id <> v.Id THEN N' (khac ID seed trong IdentityCatalog.cs)' ELSE N'' END) END
    FROM (VALUES
            (N'MANAGER', CAST('56e070a0-9b9e-49f5-8000-000000000002' AS uniqueidentifier)),
            (N'STAFF', CAST('56e070a0-9b9e-49f5-8000-000000000003' AS uniqueidentifier))) v(Ten, Id)
    LEFT JOIN [identity].[AspNetRoles] r ON r.NormalizedName = v.Ten COLLATE DATABASE_DEFAULT
    ORDER BY v.Ten;

    -- B3. Quyen salesday.manage chi thuoc Manager (Admin va Staff khong duoc mo ngay)
    SELECT @Tam = STRING_AGG(r.Name, N',') WITHIN GROUP (ORDER BY r.Name)
    FROM [identity].[RolePermission] rp
    JOIN [identity].[AspNetRoles] r ON r.Id = rp.RoleId
    WHERE rp.PermissionCode = 'salesday.manage';

    IF NOT EXISTS (SELECT 1 FROM [identity].[Permission] WHERE Code = 'salesday.manage')
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('B3', 'KHONG DAT', N'Khong co quyen salesday.manage trong identity.Permission.');
    ELSE IF @Tam = N'Manager'
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('B3', 'DAT', N'Quyen salesday.manage chi gan cho vai tro Manager.');
    ELSE
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('B3', 'KHONG DAT',
            CONCAT(N'Quyen salesday.manage dang gan cho: ', COALESCE(@Tam, N'(khong vai tro nao)'), N'; mong doi chi Manager.'));

    -- B4. Tai khoan test: ton tai, dang hoat dong, dung vai tro/quyen, chuoi bam hop le
    DROP TABLE IF EXISTS #TaiKhoan;
    CREATE TABLE #TaiKhoan (
        UserName nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
        VaiTroMongDoi nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL,
        CoQuyenMongDoi bit NOT NULL,
        UserId uniqueidentifier NULL,
        IsActive bit NULL,
        VaiTroThucTe nvarchar(1000) COLLATE DATABASE_DEFAULT NULL,
        CoQuyenThucTe bit NULL,
        DoDaiBam int NULL,
        TienToBam bit NULL,
        HaiLop bit NULL,
        DangBiKhoa bit NULL);

    INSERT #TaiKhoan (UserName, VaiTroMongDoi, CoQuyenMongDoi)
    SELECT UserName, VaiTro, CoQuyenMoNgay FROM @TaiKhoanTest;

    -- Cot IsActive do migration IdentityLoginSessions them; dung SQL dong de script van chay neu thieu cot.
    SET @Sql = CONCAT(CAST(N'' AS nvarchar(max)), N'
        UPDATE t SET
            UserId = u.[Id],
            IsActive = ', CASE WHEN COL_LENGTH(N'identity.AspNetUsers', N'IsActive') IS NULL THEN N'NULL' ELSE N'u.[IsActive]' END, N',
            VaiTroThucTe = (
                SELECT STRING_AGG(r.[Name], N'','') WITHIN GROUP (ORDER BY r.[Name])
                FROM [identity].[AspNetUserRoles] ur
                JOIN [identity].[AspNetRoles] r ON r.[Id] = ur.[RoleId]
                WHERE ur.[UserId] = u.[Id]),
            CoQuyenThucTe = CASE WHEN EXISTS (
                SELECT 1 FROM [identity].[AspNetUserRoles] ur
                JOIN [identity].[RolePermission] rp ON rp.[RoleId] = ur.[RoleId]
                WHERE ur.[UserId] = u.[Id] AND rp.[PermissionCode] = ''salesday.manage'') THEN 1 ELSE 0 END,
            DoDaiBam = DATALENGTH(u.[PasswordHash]) / 2,
            TienToBam = CASE WHEN LEFT(u.[PasswordHash], 12) COLLATE Latin1_General_BIN = N''AQAAAAIAAYag'' THEN 1 ELSE 0 END,
            HaiLop = u.[TwoFactorEnabled],
            DangBiKhoa = CASE WHEN u.[LockoutEnd] > SYSDATETIMEOFFSET() THEN 1 ELSE 0 END
        FROM #TaiKhoan t
        JOIN [identity].[AspNetUsers] u ON u.[NormalizedUserName] = UPPER(t.UserName);');
    EXEC sys.sp_executesql @Sql;

    INSERT @KQ (Ma, KetQua, NoiDung)
    SELECT 'B4',
        CASE
            WHEN t.UserId IS NULL THEN 'CANH BAO'
            WHEN t.IsActive = 1 AND t.HaiLop = 0 AND t.DangBiKhoa = 0
                AND CHARINDEX(N',' + t.VaiTroMongDoi + N',', N',' + COALESCE(t.VaiTroThucTe, N'') + N',') > 0
                AND t.CoQuyenThucTe = t.CoQuyenMongDoi AND t.DoDaiBam = 84 AND t.TienToBam = 1 THEN 'DAT'
            ELSE 'KHONG DAT' END,
        CASE WHEN t.UserId IS NULL
            THEN CONCAT(t.UserName, N': chua co tai khoan. Tao bang 02-tao-tai-khoan-test.sql (can Hai/Hieu duyet).')
            ELSE CONCAT(t.UserName, N': vai tro ', COALESCE(t.VaiTroThucTe, N'(khong co)'),
                N' (mong doi ', t.VaiTroMongDoi, N'), salesday.manage=', t.CoQuyenThucTe,
                N' (mong doi ', t.CoQuyenMongDoi, N'), IsActive=', COALESCE(CONVERT(nvarchar(1), t.IsActive), N'?'),
                N', dang bi khoa=', t.DangBiKhoa, N', 2FA=', t.HaiLop,
                N', chuoi bam ', COALESCE(CONVERT(nvarchar(10), t.DoDaiBam), N'NULL'), N' ky tu',
                CASE WHEN t.TienToBam = 1 THEN N' dung dinh dang V3' ELSE N' SAI dinh dang' END) END
    FROM #TaiKhoan t
    ORDER BY t.UserName;

    DROP TABLE #TaiKhoan;
END
ELSE
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('B2', 'KHONG DAT', N'Thieu bang Identity (AspNetRoles/AspNetUsers/Permission/RolePermission).');

/* ---------- C. Trang thai ngay ban truoc khi test ---------- */

IF @CauTrucDung = 1
BEGIN
    DECLARE @TongSoNgay int, @SoHoatDong int, @NgayLonNhat date, @TheoTrangThai nvarchar(1000), @NgayHoatDong nvarchar(1000);

    SET @Sql = CONCAT(CAST(N'' AS nvarchar(max)), N'
        SELECT @TongSoNgay = COUNT(*), @NgayLonNhat = MAX([BusinessDate]),
            @SoHoatDong = COALESCE(SUM(CASE WHEN [ActiveSlot] IS NOT NULL THEN 1 ELSE 0 END), 0)
        FROM [salesday].[NgayBan];

        SELECT @TheoTrangThai = STRING_AGG(CONVERT(nvarchar(max), CONCAT([Status], N''='', SoLuong)), N'', '')
        FROM (SELECT [Status], COUNT(*) AS SoLuong FROM [salesday].[NgayBan] GROUP BY [Status]) s;

        SELECT TOP (1) @NgayHoatDong = CONCAT(N''Id '', d.[Id], N'', ngay '', CONVERT(nvarchar(10), d.[BusinessDate], 23),
            N'', '', d.[Status], N'', tien dau ngay '', d.[OpeningCash], N'', mo boi '', ',
            CASE WHEN OBJECT_ID(N'[identity].[AspNetUsers]', N'U') IS NULL
                THEN N'CONVERT(nvarchar(36), d.[OpenedBy])'
                ELSE N'COALESCE((SELECT u.[UserName] FROM [identity].[AspNetUsers] u WHERE u.[Id] = d.[OpenedBy]), CONVERT(nvarchar(36), d.[OpenedBy]))' END,
            N', N'' luc '', CONVERT(nvarchar(19), CAST(SWITCHOFFSET(d.[OpenedAt], ''+07:00'') AS datetime2(0)), 120), N'' gio VN'')
        FROM [salesday].[NgayBan] d
        WHERE d.[ActiveSlot] IS NOT NULL;');

    EXEC sys.sp_executesql @Sql,
        N'@TongSoNgay int OUTPUT, @NgayLonNhat date OUTPUT, @SoHoatDong int OUTPUT, @TheoTrangThai nvarchar(1000) OUTPUT, @NgayHoatDong nvarchar(1000) OUTPUT',
        @TongSoNgay = @TongSoNgay OUTPUT, @NgayLonNhat = @NgayLonNhat OUTPUT, @SoHoatDong = @SoHoatDong OUTPUT,
        @TheoTrangThai = @TheoTrangThai OUTPUT, @NgayHoatDong = @NgayHoatDong OUTPUT;

    -- C1. So lieu de dien vao script 03
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('C1', 'THONG TIN',
        CONCAT(N'Tong so ngay ban hien co: ', @TongSoNgay, N' (dien vao @SoNgayTruocTest cua 03). Theo trang thai: ',
            COALESCE(@TheoTrangThai, N'(chua co dong nao)')));

    -- C2. Chua co ngay hoat dong thi giao dien moi hien form mo ngay
    IF @SoHoatDong = 0
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('C2', 'DAT',
            N'Chua co ngay ban hoat dong: giao dien /sales-day se hien form "Mo ngay ban".');
    ELSE
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('C2', 'KHONG DAT',
            CONCAT(N'Dang co ngay ban hoat dong: ', @NgayHoatDong,
                N'. KHONG tu chot/xoa ngay nay; bao Hai/Hieu thong nhat truoc khi nghiem thu.'));

    -- C3. Ngay kinh doanh hop le: phai sau moi ngay da co (salesday.date.invalid)
    DECLARE @NgayHopLe date = CASE WHEN @NgayLonNhat IS NULL OR @NgayLonNhat < @HomNayVN
        THEN @HomNayVN ELSE DATEADD(day, 1, @NgayLonNhat) END;
    IF @NgayHopLe = @HomNayVN
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('C3', 'DAT',
            CONCAT(N'Ngay lon nhat da co: ', COALESCE(CONVERT(nvarchar(10), @NgayLonNhat, 23), N'(khong co)'),
                N'. Ngay mac dinh tren giao dien (hom nay ', CONVERT(nvarchar(10), @HomNayVN, 23), N') hop le.'));
    ELSE
        INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('C3', 'CANH BAO',
            CONCAT(N'Ngay lon nhat da co: ', CONVERT(nvarchar(10), @NgayLonNhat, 23),
                N'. Giao dien mac dinh hom nay se bi tu choi (salesday.date.invalid); nhap ngay tu ',
                CONVERT(nvarchar(10), @NgayHopLe, 23), N' tro di.'));
END
ELSE
    INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('C1', 'KHONG DAT',
        N'Bo qua kiem tra du lieu ngay ban vi bang salesday.NgayBan khong ton tai hoac lech cau truc.');

/* ---------- Tong ket va in ket qua ---------- */

DECLARE @LoiMigration int = (SELECT COUNT(*) FROM @KQ WHERE Ma LIKE 'A%' AND KetQua = 'KHONG DAT');
DECLARE @ChuaSanSang int = (SELECT COUNT(*) FROM @KQ
    WHERE (KetQua = 'KHONG DAT') OR (Ma = 'B4' AND KetQua <> 'DAT'));

INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('TONG',
    CASE WHEN @LoiMigration = 0 THEN 'DAT' ELSE 'KHONG DAT' END,
    N'Database co migration SalesDay va cau truc NgayBan dung (A1-A6).');
INSERT @KQ (Ma, KetQua, NoiDung) VALUES ('TONG',
    CASE WHEN @ChuaSanSang = 0 THEN 'DAT' ELSE 'CANH BAO' END,
    CASE WHEN @ChuaSanSang = 0 THEN N'San sang nghiem thu: co tai khoan test, chua co ngay hoat dong.'
        ELSE N'CHUA san sang nghiem thu: xu ly cac dong KHONG DAT/CANH BAO o tren (B4, C2...) roi chay lai 01.' END);

DECLARE @i int = 1, @SoDong int = (SELECT MAX(STT) FROM @KQ), @Dong nvarchar(4000);
PRINT N'=== 01-kiem-tra-migration.sql ===';
WHILE @i <= @SoDong
BEGIN
    SELECT @Dong = CONCAT(N'[', LEFT(KetQua + '         ', 9), N'] ', Ma, N' ', NoiDung) FROM @KQ WHERE STT = @i;
    PRINT @Dong;
    SET @i += 1;
END;

IF (SELECT COUNT(*) FROM @KQ WHERE KetQua = 'KHONG DAT') > 0
    THROW 50001, N'Co muc KHONG DAT. Dung nghiem thu, gui ket qua nay cho Phuong/Hai.', 1;
GO
