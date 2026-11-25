using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Domain;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;

namespace Restaurant.Modules.Seating.Infrastructure;

public sealed class SeatingCatalog(SeatingDbContext db, IBusinessAudit audit) : ISeatingCatalog
{
    private static AreaDto Map(KhuVuc a) => new(a.Id, a.Ma, a.Ten, a.HoatDong, SeatingRules.EncodeVersion(a.Version));

    private static TableDto Map(Ban t) =>
        new(t.Id, t.KhuVucId, t.Ma, t.Ten, t.HoatDong, t.MaQrCongKhai, SeatingRules.EncodeVersion(t.Version));

    public async Task<IReadOnlyList<AreaDto>> ListAreasAsync(CancellationToken ct) =>
        (await db.Areas.AsNoTracking().OrderBy(x => x.Ma).ToListAsync(ct)).Select(Map).ToList();

    public async Task<IReadOnlyList<TableDto>> ListTablesAsync(CancellationToken ct) =>
        (await db.Tables.AsNoTracking().OrderBy(x => x.Ma).ToListAsync(ct)).Select(Map).ToList();

    public async Task<SeatingResult<AreaDto>> CreateAreaAsync(CreateAreaRequest request, CancellationToken ct)
    {
        if (request is null || !SeatingRules.TryCode(request.Code, out var code) ||
            !SeatingRules.TryName(request.Name, out var name))
            return SeatingResult<AreaDto>.Fail(SeatingErrors.InputInvalid);
        if (await db.Areas.AnyAsync(x => x.Ma == code, ct))
            return SeatingResult<AreaDto>.Fail(SeatingErrors.AreaCodeConflict);
        var area = new KhuVuc { Ma = code, Ten = name, HoatDong = true };
        db.Areas.Add(area);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && SeatingSql.IsUniqueViolation(sql))
        {
            return SeatingResult<AreaDto>.Fail(SeatingErrors.AreaCodeConflict);
        }
        await audit.AppendAsync(new("Seating", "area.create", "KhuVuc", SeatingRules.Number(area.Id)), ct);
        await tx.CommitAsync(ct);
        return SeatingResult<AreaDto>.Ok(Map(area));
    }

    public Task<SeatingResult<AreaDto>> UpdateAreaAsync(long id, UpdateAreaRequest request, CancellationToken ct) =>
        GuardedAsync<AreaDto>(async () =>
        {
            if (id <= 0 || request is null || !SeatingRules.TryName(request.Name, out var name) ||
                !SeatingRules.TryDecodeVersion(request.Version, out var version))
                return SeatingResult<AreaDto>.Fail(SeatingErrors.InputInvalid);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var area = await db.LockAreaAsync(id, ct);
            if (area is null) return SeatingResult<AreaDto>.Fail(SeatingErrors.AreaNotFound);
            if (!area.HoatDong) return SeatingResult<AreaDto>.Fail(SeatingErrors.AreaInactive);
            if (!area.Version.AsSpan().SequenceEqual(version))
                return SeatingResult<AreaDto>.Fail(SeatingErrors.ConcurrencyConflict);
            if (string.Equals(area.Ten, name, StringComparison.Ordinal))
            {
                await tx.CommitAsync(ct);
                return SeatingResult<AreaDto>.Ok(Map(area));
            }
            area.Ten = name;
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(new("Seating", "area.update", "KhuVuc", SeatingRules.Number(area.Id)), ct);
            await tx.CommitAsync(ct);
            return SeatingResult<AreaDto>.Ok(Map(area));
        });

    public Task<SeatingResult<AreaDto>> ArchiveAreaAsync(long id, ArchiveRequest request, CancellationToken ct) =>
        GuardedAsync<AreaDto>(async () =>
        {
            if (id <= 0 || !SeatingRules.TryDecodeVersion(request?.Version, out var version))
                return SeatingResult<AreaDto>.Fail(SeatingErrors.InputInvalid);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var area = await db.LockAreaAsync(id, ct);
            if (area is null) return SeatingResult<AreaDto>.Fail(SeatingErrors.AreaNotFound);
            if (!area.HoatDong) return SeatingResult<AreaDto>.Ok(Map(area));
            if (!area.Version.AsSpan().SequenceEqual(version))
                return SeatingResult<AreaDto>.Fail(SeatingErrors.ConcurrencyConflict);
            // Creating or moving a table takes this same area lock, so no active table can appear meanwhile.
            if (await db.Tables.AnyAsync(x => x.KhuVucId == id && x.HoatDong, ct))
                return SeatingResult<AreaDto>.Fail(SeatingErrors.AreaHasActiveTables);
            area.HoatDong = false;
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(new("Seating", "area.archive", "KhuVuc", SeatingRules.Number(area.Id)), ct);
            await tx.CommitAsync(ct);
            return SeatingResult<AreaDto>.Ok(Map(area));
        });

    public Task<SeatingResult<TableDto>> CreateTableAsync(CreateTableRequest request, CancellationToken ct) =>
        GuardedAsync<TableDto>(async () =>
        {
            if (request is null || request.AreaId <= 0 || !SeatingRules.TryCode(request.Code, out var code) ||
                !SeatingRules.TryName(request.Name, out var name))
                return SeatingResult<TableDto>.Fail(SeatingErrors.InputInvalid);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var area = await db.LockAreaAsync(request.AreaId, ct);
            if (area is null) return SeatingResult<TableDto>.Fail(SeatingErrors.AreaNotFound);
            if (!area.HoatDong) return SeatingResult<TableDto>.Fail(SeatingErrors.AreaInactive);
            if (await db.Tables.AnyAsync(x => x.Ma == code, ct))
                return SeatingResult<TableDto>.Fail(SeatingErrors.TableCodeConflict);
            var table = new Ban
            {
                KhuVucId = area.Id,
                Ma = code,
                Ten = name,
                HoatDong = true,
                MaQrCongKhai = SeatingRules.NewTableLocator()
            };
            db.Tables.Add(table);
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(new("Seating", "table.create", "Ban", SeatingRules.Number(table.Id)), ct);
            await tx.CommitAsync(ct);
            return SeatingResult<TableDto>.Ok(Map(table));
        });

    public Task<SeatingResult<TableDto>> UpdateTableAsync(long id, UpdateTableRequest request, CancellationToken ct) =>
        GuardedAsync<TableDto>(async () =>
        {
            if (id <= 0 || request is null || request.AreaId <= 0 || !SeatingRules.TryName(request.Name, out var name) ||
                !SeatingRules.TryDecodeVersion(request.Version, out var version))
                return SeatingResult<TableDto>.Fail(SeatingErrors.InputInvalid);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var table = await db.LockTableAsync(id, ct);
            if (table is null) return SeatingResult<TableDto>.Fail(SeatingErrors.TableNotFound);
            if (!table.HoatDong) return SeatingResult<TableDto>.Fail(SeatingErrors.TableInactive);
            if (!table.Version.AsSpan().SequenceEqual(version))
                return SeatingResult<TableDto>.Fail(SeatingErrors.ConcurrencyConflict);
            if (request.AreaId == table.KhuVucId && string.Equals(table.Ten, name, StringComparison.Ordinal))
            {
                await tx.CommitAsync(ct);
                return SeatingResult<TableDto>.Ok(Map(table));
            }
            if (request.AreaId != table.KhuVucId)
            {
                var area = await db.LockAreaAsync(request.AreaId, ct);
                if (area is null) return SeatingResult<TableDto>.Fail(SeatingErrors.AreaNotFound);
                if (!area.HoatDong) return SeatingResult<TableDto>.Fail(SeatingErrors.AreaInactive);
                table.KhuVucId = area.Id;
            }
            table.Ten = name;
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(new("Seating", "table.update", "Ban", SeatingRules.Number(table.Id)), ct);
            await tx.CommitAsync(ct);
            return SeatingResult<TableDto>.Ok(Map(table));
        });

    public Task<SeatingResult<TableDto>> ArchiveTableAsync(long id, ArchiveRequest request, CancellationToken ct) =>
        GuardedAsync<TableDto>(async () =>
        {
            if (id <= 0 || !SeatingRules.TryDecodeVersion(request?.Version, out var version))
                return SeatingResult<TableDto>.Fail(SeatingErrors.InputInvalid);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var table = await db.LockTableAsync(id, ct);
            if (table is null) return SeatingResult<TableDto>.Fail(SeatingErrors.TableNotFound);
            if (!table.HoatDong) return SeatingResult<TableDto>.Ok(Map(table));
            if (!table.Version.AsSpan().SequenceEqual(version))
                return SeatingResult<TableDto>.Fail(SeatingErrors.ConcurrencyConflict);
            // Opening a session takes this same table lock, so a session cannot start while we archive.
            if (await db.Sessions.AnyAsync(x => x.BanHienTaiId == id && x.DongLuc == null, ct))
                return SeatingResult<TableDto>.Fail(SeatingErrors.TableInUse);
            table.HoatDong = false;
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(new("Seating", "table.archive", "Ban", SeatingRules.Number(table.Id)), ct);
            await tx.CommitAsync(ct);
            return SeatingResult<TableDto>.Ok(Map(table));
        });

    public async Task<IReadOnlyList<BoardAreaDto>> BoardAsync(CancellationToken ct)
    {
        var rows = await (
            from t in db.Tables.AsNoTracking()
            join a in db.Areas.AsNoTracking() on t.KhuVucId equals a.Id
            where t.HoatDong && a.HoatDong
            join s in db.Sessions.AsNoTracking().Where(x => x.DongLuc == null)
                on t.Id equals s.BanHienTaiId into open
            from s in open.DefaultIfEmpty()
            orderby a.Ma, t.Ma
            select new
            {
                AreaId = a.Id,
                AreaCode = a.Ma,
                AreaName = a.Ten,
                TableId = t.Id,
                TableCode = t.Ma,
                TableName = t.Ten,
                SessionId = (long?)s.Id,
                OpenedAt = (DateTime?)s.MoLuc
            }).ToListAsync(ct);
        return rows.GroupBy(x => new { x.AreaId, x.AreaCode, x.AreaName })
            .Select(g => new BoardAreaDto(g.Key.AreaId, g.Key.AreaCode, g.Key.AreaName,
                g.Select(x => new BoardTableDto(x.TableId, x.TableCode, x.TableName,
                    x.SessionId is null ? BoardStatuses.Free : BoardStatuses.InUse,
                    x.SessionId is null ? null : new BoardSessionDto(x.SessionId.Value, SeatingRules.Utc(x.OpenedAt!.Value)))).ToList()))
            .ToList();
    }

    /// <summary>Maps lost races (stale row version, duplicate code, deadlock) to stable business errors.</summary>
    private async Task<SeatingResult<T>> GuardedAsync<T>(Func<Task<SeatingResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (DbUpdateConcurrencyException)
        {
            return SeatingResult<T>.Fail(SeatingErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql)
        {
            if (SeatingSql.IsUniqueViolation(sql))
                return SeatingResult<T>.Fail(typeof(T) == typeof(AreaDto)
                    ? SeatingErrors.AreaCodeConflict : SeatingErrors.TableCodeConflict);
            if (sql.Number == SeatingSql.Deadlock) return SeatingResult<T>.Fail(SeatingErrors.ConcurrencyConflict);
            throw;
        }
        catch (SqlException ex) when (ex.Number == SeatingSql.Deadlock)
        {
            return SeatingResult<T>.Fail(SeatingErrors.ConcurrencyConflict);
        }
    }
}
