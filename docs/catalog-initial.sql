IF OBJECT_ID(N'[catalog].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'catalog') IS NULL EXEC(N'CREATE SCHEMA [catalog];');
    CREATE TABLE [catalog].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    IF SCHEMA_ID(N'catalog') IS NULL EXEC(N'CREATE SCHEMA [catalog];');
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE TABLE [catalog].[NhomMon] (
        [Id] uniqueidentifier NOT NULL,
        [Ten] nvarchar(200) NOT NULL,
        [ThuTu] int NOT NULL,
        [HoatDong] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_NhomMon] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE TABLE [catalog].[NhomTuyChon] (
        [Id] uniqueidentifier NOT NULL,
        [Ten] nvarchar(200) NOT NULL,
        [Loai] nvarchar(16) NOT NULL,
        [HoatDong] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_NhomTuyChon] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_NhomTuyChon_Loai] CHECK ([Loai] IN ('TOPPING','TUY_CHON'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE TABLE [catalog].[Mon] (
        [Id] uniqueidentifier NOT NULL,
        [NhomMonId] uniqueidentifier NOT NULL,
        [Ma] nvarchar(50) NOT NULL,
        [Ten] nvarchar(200) NOT NULL,
        [Tram] nvarchar(8) NOT NULL,
        [AnhObjectKey] nvarchar(512) NULL,
        [HoatDong] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Mon] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Mon_Tram] CHECK ([Tram] IN ('BEP','BAR')),
        CONSTRAINT [FK_Mon_NhomMon_NhomMonId] FOREIGN KEY ([NhomMonId]) REFERENCES [catalog].[NhomMon] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE TABLE [catalog].[TuyChon] (
        [Id] uniqueidentifier NOT NULL,
        [NhomTuyChonId] uniqueidentifier NOT NULL,
        [Ten] nvarchar(200) NOT NULL,
        [GiaCong] bigint NOT NULL,
        [HoatDong] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_TuyChon] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TuyChon_Gia] CHECK ([GiaCong] >= 0),
        CONSTRAINT [FK_TuyChon_NhomTuyChon_NhomTuyChonId] FOREIGN KEY ([NhomTuyChonId]) REFERENCES [catalog].[NhomTuyChon] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE TABLE [catalog].[BienThe] (
        [Id] uniqueidentifier NOT NULL,
        [MonId] uniqueidentifier NOT NULL,
        [Ma] nvarchar(50) NOT NULL,
        [Ten] nvarchar(200) NOT NULL,
        [Gia] bigint NOT NULL,
        [HoatDong] bit NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_BienThe] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_BienThe_Gia] CHECK ([Gia] >= 0),
        CONSTRAINT [FK_BienThe_Mon_MonId] FOREIGN KEY ([MonId]) REFERENCES [catalog].[Mon] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE TABLE [catalog].[HetMon] (
        [NgayBanId] uniqueidentifier NOT NULL,
        [MonId] uniqueidentifier NOT NULL,
        [Het] bit NOT NULL,
        [CapNhatLuc] datetimeoffset NOT NULL,
        [NguoiCapNhatId] uniqueidentifier NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_HetMon] PRIMARY KEY ([NgayBanId], [MonId]),
        CONSTRAINT [FK_HetMon_Mon_MonId] FOREIGN KEY ([MonId]) REFERENCES [catalog].[Mon] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE TABLE [catalog].[MonNhomTuyChon] (
        [MonId] uniqueidentifier NOT NULL,
        [NhomTuyChonId] uniqueidentifier NOT NULL,
        [ChonToiThieu] int NOT NULL,
        [ChonToiDa] int NOT NULL,
        CONSTRAINT [PK_MonNhomTuyChon] PRIMARY KEY ([MonId], [NhomTuyChonId]),
        CONSTRAINT [CK_MonNhomTuyChon_Range] CHECK ([ChonToiThieu] >= 0 AND [ChonToiDa] >= [ChonToiThieu]),
        CONSTRAINT [FK_MonNhomTuyChon_Mon_MonId] FOREIGN KEY ([MonId]) REFERENCES [catalog].[Mon] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MonNhomTuyChon_NhomTuyChon_NhomTuyChonId] FOREIGN KEY ([NhomTuyChonId]) REFERENCES [catalog].[NhomTuyChon] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE TABLE [catalog].[HetTuyChon] (
        [NgayBanId] uniqueidentifier NOT NULL,
        [TuyChonId] uniqueidentifier NOT NULL,
        [Het] bit NOT NULL,
        [CapNhatLuc] datetimeoffset NOT NULL,
        [NguoiCapNhatId] uniqueidentifier NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_HetTuyChon] PRIMARY KEY ([NgayBanId], [TuyChonId]),
        CONSTRAINT [FK_HetTuyChon_TuyChon_TuyChonId] FOREIGN KEY ([TuyChonId]) REFERENCES [catalog].[TuyChon] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE TABLE [catalog].[HetBienThe] (
        [NgayBanId] uniqueidentifier NOT NULL,
        [BienTheId] uniqueidentifier NOT NULL,
        [Het] bit NOT NULL,
        [CapNhatLuc] datetimeoffset NOT NULL,
        [NguoiCapNhatId] uniqueidentifier NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_HetBienThe] PRIMARY KEY ([NgayBanId], [BienTheId]),
        CONSTRAINT [FK_HetBienThe_BienThe_BienTheId] FOREIGN KEY ([BienTheId]) REFERENCES [catalog].[BienThe] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BienThe_MonId_Ma] ON [catalog].[BienThe] ([MonId], [Ma]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_HetBienThe_BienTheId] ON [catalog].[HetBienThe] ([BienTheId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_HetMon_MonId] ON [catalog].[HetMon] ([MonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_HetTuyChon_TuyChonId] ON [catalog].[HetTuyChon] ([TuyChonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Mon_Ma] ON [catalog].[Mon] ([Ma]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_Mon_NhomMonId] ON [catalog].[Mon] ([NhomMonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_MonNhomTuyChon_NhomTuyChonId] ON [catalog].[MonNhomTuyChon] ([NhomTuyChonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    CREATE INDEX [IX_TuyChon_NhomTuyChonId] ON [catalog].[TuyChon] ([NhomTuyChonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074315_InitialCatalog'
)
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110074315_InitialCatalog', N'10.0.8');
END;

COMMIT;
GO

