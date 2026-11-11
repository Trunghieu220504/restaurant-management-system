IF OBJECT_ID(N'[salesday].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'salesday') IS NULL EXEC(N'CREATE SCHEMA [salesday];');
    CREATE TABLE [salesday].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [salesday].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074313_InitialSalesDay'
)
BEGIN
    IF SCHEMA_ID(N'salesday') IS NULL EXEC(N'CREATE SCHEMA [salesday];');
END;

IF NOT EXISTS (
    SELECT * FROM [salesday].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074313_InitialSalesDay'
)
BEGIN
    CREATE TABLE [salesday].[NgayBan] (
        [Id] uniqueidentifier NOT NULL,
        [BusinessDate] date NOT NULL,
        [OpenedAt] datetimeoffset NOT NULL,
        [OpenedBy] uniqueidentifier NOT NULL,
        [OpeningCash] bigint NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [ActiveSlot] int NULL,
        [RequestKey] uniqueidentifier NOT NULL,
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_NgayBan] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_NgayBan_Cash] CHECK ([OpeningCash] >= 0),
        CONSTRAINT [CK_NgayBan_State] CHECK (([Status] IN ('DANG_BAN','CHO_CHOT') AND [ActiveSlot] = 1 AND [ActiveSlot] IS NOT NULL) OR ([Status] = 'DA_CHOT' AND [ActiveSlot] IS NULL))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [salesday].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074313_InitialSalesDay'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_NgayBan_ActiveSlot] ON [salesday].[NgayBan] ([ActiveSlot]) WHERE [ActiveSlot] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [salesday].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074313_InitialSalesDay'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NgayBan_BusinessDate] ON [salesday].[NgayBan] ([BusinessDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [salesday].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074313_InitialSalesDay'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NgayBan_RequestKey] ON [salesday].[NgayBan] ([RequestKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [salesday].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261110074313_InitialSalesDay'
)
BEGIN
    INSERT INTO [salesday].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110074313_InitialSalesDay', N'10.0.8');
END;

COMMIT;
GO

