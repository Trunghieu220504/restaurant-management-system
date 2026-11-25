using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Infrastructure;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;
using Restaurant.Shared.SqlServer;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class SeatingBusinessAuditSqlServerTests : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantSeatingAuditTest_" + Guid.NewGuid().ToString("N");
    private readonly Guid actor = Guid.NewGuid();
    private readonly SeatingTestDayDirectory days = new(Guid.NewGuid());
    private readonly List<BusinessAuditEntry> entries = [];
    private string? connection;
    private ServiceProvider? provider;
    private bool failAudit;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        connection = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Restaurant"] = connection }).Build());
        services.AddSeatingModule();
        services.AddSingleton<IBusinessDayDirectory>(days);
        services.AddScoped<IBusinessAudit>(sp => new RecordingAudit(sp.GetRequiredService<SeatingDbContext>(), this));
        provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SeatingDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (provider is null) return;
        if (connection is null || new SqlConnectionStringBuilder(connection).InitialCatalog != databaseName ||
            !databaseName.StartsWith("RestaurantSeatingAuditTest_", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<SeatingDbContext>().Database.EnsureDeletedAsync();
        await provider.DisposeAsync();
    }

    private async Task<T> WithAsync<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        await using var scope = provider!.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private async Task<(TableDto First, TableDto Second)> TablesAsync()
    {
        var area = (await WithAsync<ISeatingCatalog, SeatingResult<AreaDto>>(s =>
            s.CreateAreaAsync(new("A", "Area"), default))).Value!;
        var first = (await WithAsync<ISeatingCatalog, SeatingResult<TableDto>>(s =>
            s.CreateTableAsync(new(area.Id, "T1", "First"), default))).Value!;
        var second = (await WithAsync<ISeatingCatalog, SeatingResult<TableDto>>(s =>
            s.CreateTableAsync(new(area.Id, "T2", "Second"), default))).Value!;
        return (first, second);
    }

    private Task<SeatingResult<SessionDto>> OpenAsync(long table, Guid key) =>
        WithAsync<ISeatingSessions, SeatingResult<SessionDto>>(s => s.OpenAsync(table, actor, key, default));

    private Task<SeatingResult<GuestInvite>> InviteAsync(long session) =>
        WithAsync<IGuestAccess, SeatingResult<GuestInvite>>(s => s.InviteAsync(session, actor, default));

    private Task<SeatingResult<GuestToken>> ExchangeAsync(GuestInvite invite) =>
        WithAsync<IGuestAccess, SeatingResult<GuestToken>>(s => s.ExchangeAsync(new(invite.TableCode, invite.JoinCode), default));

    [SqlServerFact]
    public async Task Catalog_records_each_change_and_skips_archive_replay()
    {
        var (first, second) = await TablesAsync();
        var area = await WithAsync<ISeatingCatalog, SeatingResult<AreaDto>>(s =>
            s.UpdateAreaAsync(first.AreaId, new("Renamed", "invalid"), default));
        Assert.Equal(SeatingErrors.InputInvalid, area.Error);
        Assert.Equal(3, entries.Count);
        var current = Assert.Single(await WithAsync<ISeatingCatalog, IReadOnlyList<AreaDto>>(s => s.ListAreasAsync(default)));
        var updated = await WithAsync<ISeatingCatalog, SeatingResult<AreaDto>>(s =>
            s.UpdateAreaAsync(current.Id, new("Renamed", current.Version), default));
        Assert.Null(updated.Error);
        var areaNoOp = await WithAsync<ISeatingCatalog, SeatingResult<AreaDto>>(s =>
            s.UpdateAreaAsync(current.Id, new("Renamed", updated.Value!.Version), default));
        Assert.Null(areaNoOp.Error);
        var renamed = await WithAsync<ISeatingCatalog, SeatingResult<TableDto>>(s =>
            s.UpdateTableAsync(first.Id, new(first.AreaId, "Renamed table", first.Version), default));
        Assert.Null(renamed.Error);
        var tableNoOp = await WithAsync<ISeatingCatalog, SeatingResult<TableDto>>(s =>
            s.UpdateTableAsync(first.Id, new(first.AreaId, "Renamed table", renamed.Value!.Version), default));
        Assert.Null(tableNoOp.Error);
        foreach (var table in new[] { renamed.Value!, second })
        {
            var archived = await WithAsync<ISeatingCatalog, SeatingResult<TableDto>>(s =>
                s.ArchiveTableAsync(table.Id, new(table.Version), default));
            Assert.Null(archived.Error);
            Assert.Null((await WithAsync<ISeatingCatalog, SeatingResult<TableDto>>(s =>
                s.ArchiveTableAsync(table.Id, new(table.Version), default))).Error);
        }
        Assert.Null((await WithAsync<ISeatingCatalog, SeatingResult<AreaDto>>(s =>
            s.ArchiveAreaAsync(current.Id, new(updated.Value!.Version), default))).Error);
        Assert.Null((await WithAsync<ISeatingCatalog, SeatingResult<AreaDto>>(s =>
            s.ArchiveAreaAsync(current.Id, new(updated.Value!.Version), default))).Error);
        Assert.Equal(new[] { "area.create", "table.create", "table.create", "area.update", "table.update",
            "table.archive", "table.archive", "area.archive" }, entries.Select(x => x.Action));
        Assert.All(entries, entry =>
        {
            Assert.Equal("Seating", entry.SourceModule);
            Assert.True(long.Parse(entry.EntityId, CultureInfo.InvariantCulture) > 0);
            Assert.Null(entry.BusinessDayId);
        });
    }

    [SqlServerFact]
    public async Task Session_commands_audit_once_with_actor_day_and_transfer_reason()
    {
        var (first, second) = await TablesAsync();
        entries.Clear();
        var openKey = Guid.NewGuid();
        var opened = await OpenAsync(first.Id, openKey);
        Assert.Null(opened.Error);
        Assert.True((await OpenAsync(first.Id, openKey)).Replayed);
        var session = opened.Value!.Id;
        var transferKey = Guid.NewGuid();
        Task<SeatingResult<SessionDto>> Transfer() => WithAsync<ISeatingSessions, SeatingResult<SessionDto>>(s =>
            s.TransferAsync(session, new(second.Id, "  Window seat  "), actor, transferKey, default));
        Assert.Null((await Transfer()).Error);
        Assert.True((await Transfer()).Replayed);
        var closeKey = Guid.NewGuid();
        Task<SeatingResult<SessionDto>> Close() => WithAsync<ISeatingSessions, SeatingResult<SessionDto>>(s =>
            s.CloseAsync(session, actor, closeKey, default));
        Assert.Null((await Close()).Error);
        days.Open = false;
        Assert.True((await Close()).Replayed);
        Assert.True((await OpenAsync(first.Id, openKey)).Replayed);
        Assert.Equal(new[] { "MO_PHIEN", "CHUYEN_BAN", "DONG_PHIEN" }, entries.Select(x => x.Action));
        Assert.All(entries, entry =>
        {
            Assert.Equal("Seating", entry.SourceModule);
            Assert.Equal("PhienSuDung", entry.EntityType);
            Assert.Equal(session.ToString(CultureInfo.InvariantCulture), entry.EntityId);
            Assert.Equal(actor, entry.ActorId);
            Assert.Equal(days.Day, entry.BusinessDayId);
        });
        Assert.Equal("Window seat", entries[1].Reason);
    }

    [SqlServerFact]
    public async Task Audit_failure_rolls_back_area_and_session_with_idempotency_record()
    {
        failAudit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => WithAsync<ISeatingCatalog, SeatingResult<AreaDto>>(s =>
            s.CreateAreaAsync(new("A", "Area"), default)));
        Assert.Equal(0, await WithAsync<SeatingDbContext, int>(db => db.Areas.CountAsync()));
        failAudit = false;
        var (first, _) = await TablesAsync();
        var key = Guid.NewGuid();
        failAudit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => OpenAsync(first.Id, key));
        Assert.Equal(0, await WithAsync<SeatingDbContext, int>(db => db.Sessions.CountAsync()));
        Assert.Equal(0, await WithAsync<SeatingDbContext, int>(db => db.Requests.CountAsync()));
        failAudit = false;
        var retry = await OpenAsync(first.Id, key);
        Assert.Null(retry.Error);
        Assert.False(retry.Replayed);
    }

    [SqlServerFact]
    public async Task Qr_audit_uses_entity_ids_and_failure_rolls_back_invite_and_exchange()
    {
        var (first, _) = await TablesAsync();
        var session = (await OpenAsync(first.Id, Guid.NewGuid())).Value!.Id;
        entries.Clear();
        failAudit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => InviteAsync(session));
        Assert.Equal(0, await WithAsync<SeatingDbContext, int>(db => db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM seating.MaThamGiaPhien").SingleAsync()));
        failAudit = false;
        var invite = (await InviteAsync(session)).Value!;
        failAudit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExchangeAsync(invite));
        Assert.Equal(0, await WithAsync<SeatingDbContext, int>(db => db.Grants.CountAsync()));
        failAudit = false;
        var token = await ExchangeAsync(invite);
        Assert.Null(token.Error);
        Assert.Equal("qr.code.used", (await ExchangeAsync(invite)).Error);
        Assert.Equal(2, entries.Count);
        Assert.Equal(new BusinessAuditEntry("Seating", "qr.invite", "PhienSuDung",
            session.ToString(CultureInfo.InvariantCulture), actor, days.Day), entries[0]);
        var grantId = await WithAsync<SeatingDbContext, Guid>(db => db.Grants.Select(x => x.Id).SingleAsync());
        Assert.Equal(new BusinessAuditEntry("Seating", "qr.exchange", "QuyenTruyCapKhach",
            grantId.ToString("D"), BusinessDayId: days.Day, ResolveActor: false), entries[1]);
    }

    [SqlServerFact]
    public async Task Qr_day_gate_rejects_writes_without_consuming_code_and_preserves_errors()
    {
        var (first, _) = await TablesAsync();
        var session = (await OpenAsync(first.Id, Guid.NewGuid())).Value!.Id;
        var invite = (await InviteAsync(session)).Value!;
        entries.Clear();
        days.Open = false;
        Assert.Equal(SeatingErrors.DayNotOpen, (await InviteAsync(session)).Error);
        Assert.Equal(SeatingErrors.DayNotOpen, (await ExchangeAsync(invite)).Error);
        Assert.Empty(entries);
        days.Open = true;
        Assert.Null((await ExchangeAsync(invite)).Error);
        days.Open = false;
        Assert.Equal("qr.code.used", (await ExchangeAsync(invite)).Error);
        await WithAsync<SeatingDbContext, int>(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE seating.MaThamGiaPhien SET TaoLuc=DATEADD(hour,-2,SYSUTCDATETIME()), HetHanLuc=DATEADD(hour,-1,SYSUTCDATETIME())"));
        Assert.Equal("qr.code.expired", (await ExchangeAsync(invite)).Error);
        await WithAsync<SeatingDbContext, int>(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE seating.MaThamGiaPhien SET ThuHoiLuc=SYSUTCDATETIME()"));
        Assert.Equal("qr.code.invalid", (await ExchangeAsync(invite)).Error);
        Assert.Single(entries);
    }

    private sealed class RecordingAudit(SeatingDbContext db, SeatingBusinessAuditSqlServerTests owner) : IBusinessAudit
    {
        public Task AppendAsync(BusinessAuditEntry entry, CancellationToken cancellationToken)
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            Assert.False(db.ChangeTracker.HasChanges());
            if (owner.failAudit) throw new InvalidOperationException("Simulated audit failure.");
            owner.entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}

internal sealed class SeatingTestDayDirectory(Guid day) : IBusinessDayDirectory
{
    public Guid Day { get; } = day;
    public bool Open { get; set; } = true;
    public Task<Guid?> CurrentOpenDayAsync(CancellationToken ct) => Task.FromResult<Guid?>(Open ? Day : null);

    public async Task<bool> RequireOpenAsync(DbTransaction transaction, Guid dayId, CancellationToken ct)
    {
        await BusinessDayLock.AcquireAsync(transaction.Connection!, transaction, dayId,
            BusinessDayLockMode.Shared, cancellationToken: ct);
        return Open && dayId == Day;
    }
}
