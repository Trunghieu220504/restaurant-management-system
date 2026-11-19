using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Domain;
using Restaurant.Modules.Seating.Infrastructure.Persistence;

namespace Restaurant.Modules.Seating.Infrastructure;

/// <summary>
/// Sessions follow the lock order of ADR-01: business day (Shared), then the table row, then the session.
/// Two writers can never leave a table with two open sessions: the filtered unique index on
/// PhienSuDung(BanHienTaiId) WHERE DongLuc IS NULL is the final guard behind the explicit checks.
/// </summary>
public sealed class SeatingSessions(
    SeatingDbContext db,
    IBusinessDayDirectory days,
    ISessionClosureGuard closureGuard,
    TimeProvider clock) : ISeatingSessions
{
    private const string Done = "HOAN_TAT";

    public Task<SeatingResult<SessionDto>> OpenAsync(long tableId, Guid actor, Guid key, CancellationToken ct)
    {
        if (tableId <= 0 || actor == Guid.Empty || key == Guid.Empty)
            return Task.FromResult(SeatingResult<SessionDto>.Fail(SeatingErrors.InputInvalid));
        return RunAsync("MO_PHIEN", actor, key, $"open|{SeatingRules.Number(tableId)}",
            token => OpenCoreAsync(tableId, actor, token), ct);
    }

    public Task<SeatingResult<SessionDto>> TransferAsync(
        long sessionId, TransferRequest request, Guid actor, Guid key, CancellationToken ct)
    {
        if (sessionId <= 0 || request is null || request.ToTableId <= 0 || actor == Guid.Empty || key == Guid.Empty ||
            !SeatingRules.TryReason(request.Reason, out var reason))
            return Task.FromResult(SeatingResult<SessionDto>.Fail(SeatingErrors.InputInvalid));
        return RunAsync("CHUYEN_BAN", actor, key,
            $"transfer|{SeatingRules.Number(sessionId)}|{SeatingRules.Number(request.ToTableId)}|{reason}",
            token => TransferCoreAsync(sessionId, request.ToTableId, reason, actor, token), ct);
    }

    public Task<SeatingResult<SessionDto>> CloseAsync(long sessionId, Guid actor, Guid key, CancellationToken ct)
    {
        if (sessionId <= 0 || actor == Guid.Empty || key == Guid.Empty)
            return Task.FromResult(SeatingResult<SessionDto>.Fail(SeatingErrors.InputInvalid));
        return RunAsync("DONG_PHIEN", actor, key, $"close|{SeatingRules.Number(sessionId)}",
            token => CloseCoreAsync(sessionId, actor, token), ct);
    }

    private async Task<SeatingResult<PhienSuDung>> OpenCoreAsync(long tableId, Guid actor, CancellationToken ct)
    {
        var dayId = await days.CurrentOpenDayAsync(ct);
        if (dayId is null) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.DayNotOpen);
        var gate = await EnterDayAsync(dayId.Value, ct);
        if (gate is not null) return SeatingResult<PhienSuDung>.Fail(gate);

        var table = await db.LockTableAsync(tableId, ct);
        if (table is null) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.TableNotFound);
        if (!table.HoatDong) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.TableInactive);
        if (!await db.Areas.AsNoTracking().AnyAsync(x => x.Id == table.KhuVucId && x.HoatDong, ct))
            return SeatingResult<PhienSuDung>.Fail(SeatingErrors.AreaInactive);
        if (await db.Sessions.AnyAsync(x => x.BanHienTaiId == tableId && x.DongLuc == null, ct))
            return SeatingResult<PhienSuDung>.Fail(SeatingErrors.TableOccupied);

        var session = new PhienSuDung
        {
            BanHienTaiId = tableId,
            NgayBanId = dayId.Value,
            MoLuc = Now(),
            NguoiMoId = actor
        };
        db.Sessions.Add(session);
        return SeatingResult<PhienSuDung>.Ok(session);
    }

    private async Task<SeatingResult<PhienSuDung>> TransferCoreAsync(
        long sessionId, long toTableId, string reason, Guid actor, CancellationToken ct)
    {
        var dayId = await db.Sessions.AsNoTracking().Where(x => x.Id == sessionId)
            .Select(x => (Guid?)x.NgayBanId).SingleOrDefaultAsync(ct);
        if (dayId is null) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.SessionNotFound);
        var gate = await EnterDayAsync(dayId.Value, ct);
        if (gate is not null) return SeatingResult<PhienSuDung>.Fail(gate);

        // Loaded only after the day lock so the state we decide on cannot be stale.
        var session = await db.Sessions.SingleAsync(x => x.Id == sessionId, ct);
        if (session.DongLuc is not null) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.SessionClosed);
        if (toTableId == session.BanHienTaiId) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.TransferSameTable);

        var destination = await db.LockTableAsync(toTableId, ct);
        if (destination is null) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.TableNotFound);
        if (!destination.HoatDong) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.TableInactive);
        if (!await db.Areas.AsNoTracking().AnyAsync(x => x.Id == destination.KhuVucId && x.HoatDong, ct))
            return SeatingResult<PhienSuDung>.Fail(SeatingErrors.AreaInactive);
        if (await db.Sessions.AnyAsync(x => x.BanHienTaiId == toTableId && x.DongLuc == null, ct))
            return SeatingResult<PhienSuDung>.Fail(SeatingErrors.TableOccupied);

        // History and the move are saved together. Guest grants stay bound to the session, so they survive (C05).
        db.Transfers.Add(new ChuyenBan
        {
            PhienSuDungId = session.Id,
            BanCuId = session.BanHienTaiId,
            BanMoiId = toTableId,
            ChuyenLuc = Now(),
            NguoiChuyenId = actor,
            LyDo = reason
        });
        session.BanHienTaiId = toTableId;
        return SeatingResult<PhienSuDung>.Ok(session);
    }

    private async Task<SeatingResult<PhienSuDung>> CloseCoreAsync(long sessionId, Guid actor, CancellationToken ct)
    {
        var dayId = await db.Sessions.AsNoTracking().Where(x => x.Id == sessionId)
            .Select(x => (Guid?)x.NgayBanId).SingleOrDefaultAsync(ct);
        if (dayId is null) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.SessionNotFound);
        var gate = await EnterDayAsync(dayId.Value, ct);
        if (gate is not null) return SeatingResult<PhienSuDung>.Fail(gate);

        var session = await db.Sessions.SingleAsync(x => x.Id == sessionId, ct);
        if (session.DongLuc is not null) return SeatingResult<PhienSuDung>.Fail(SeatingErrors.SessionClosed);
        if (await closureGuard.HasOpenOrdersAsync(sessionId, ct))
            return SeatingResult<PhienSuDung>.Fail(SeatingErrors.SessionHasOpenOrders);

        var now = Now();
        session.DongLuc = now > session.MoLuc ? now : session.MoLuc;
        session.NguoiDongId = actor;
        // Closing ends every guest's access in the same transaction (test 3). Earlier revocations keep their time.
        foreach (var grant in await db.Grants.Where(x => x.PhienSuDungId == sessionId && x.ThuHoiLuc == null).ToListAsync(ct))
            grant.ThuHoiLuc = session.DongLuc;
        return SeatingResult<PhienSuDung>.Ok(session);
    }

    /// <summary>Takes the Shared day lock, then re-reads the day: a day that started closing is refused.</summary>
    private async Task<string?> EnterDayAsync(Guid dayId, CancellationToken ct)
    {
        await db.AcquireDaySharedAsync(dayId, ct);
        return await days.IsOpenAsync(dayId, ct) ? null : SeatingErrors.DayNotOpen;
    }

    private async Task<SeatingResult<SessionDto>> RunAsync(
        string operation, Guid actor, Guid key, string payload,
        Func<CancellationToken, Task<SeatingResult<PhienSuDung>>> action, CancellationToken ct)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        // Second pass only happens when a concurrent request with the same key committed first.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var prior = await db.Requests.AsNoTracking().SingleOrDefaultAsync(
                    x => x.Operation == operation && x.ActorScope == actor && x.Key == key, ct);
                if (prior is not null) return await ReplayAsync(prior, hash, ct);

                var outcome = await action(ct);
                if (outcome.Error is not null)
                {
                    await tx.RollbackAsync(ct);
                    return SeatingResult<SessionDto>.Fail(outcome.Error);
                }

                var session = outcome.Value!;
                await db.SaveChangesAsync(ct);
                db.Requests.Add(new YeuCauDaXuLy
                {
                    Operation = operation,
                    ActorScope = actor,
                    Key = key,
                    RequestHash = hash,
                    ResultReference = SeatingRules.Number(session.Id),
                    State = Done,
                    CreatedAt = Now()
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return SeatingResult<SessionDto>.Ok(Map(session));
            }
            catch (DbUpdateConcurrencyException)
            {
                return SeatingResult<SessionDto>.Fail(SeatingErrors.ConcurrencyConflict);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql)
            {
                if (SeatingSql.IsUniqueViolation(sql))
                {
                    if (sql.Message.Contains("YeuCauDaXuLy", StringComparison.Ordinal)) continue;
                    return SeatingResult<SessionDto>.Fail(SeatingErrors.TableOccupied);
                }
                if (sql.Number == SeatingSql.Deadlock) return SeatingResult<SessionDto>.Fail(SeatingErrors.ConcurrencyConflict);
                throw;
            }
            catch (SqlException ex) when (ex.Number == SeatingSql.DayLockTimeout)
            {
                return SeatingResult<SessionDto>.Fail(SeatingErrors.DayNotOpen);
            }
            catch (SqlException ex) when (ex.Number == SeatingSql.Deadlock)
            {
                return SeatingResult<SessionDto>.Fail(SeatingErrors.ConcurrencyConflict);
            }
        }
        return SeatingResult<SessionDto>.Fail(SeatingErrors.ConcurrencyConflict);
    }

    private async Task<SeatingResult<SessionDto>> ReplayAsync(YeuCauDaXuLy prior, byte[] hash, CancellationToken ct)
    {
        if (!prior.RequestHash.AsSpan().SequenceEqual(hash))
            return SeatingResult<SessionDto>.Fail(SeatingErrors.IdempotencyConflict);
        var id = long.Parse(prior.ResultReference, System.Globalization.CultureInfo.InvariantCulture);
        var session = await db.Sessions.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return SeatingResult<SessionDto>.Ok(Map(session), replayed: true);
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private static SessionDto Map(PhienSuDung s) => new(
        s.Id, s.BanHienTaiId, s.NgayBanId, SeatingRules.Utc(s.MoLuc), s.NguoiMoId,
        s.DongLuc is null ? null : SeatingRules.Utc(s.DongLuc.Value), s.NguoiDongId,
        SeatingRules.EncodeVersion(s.Version));
}

/// <summary>Default until Sales exists: no order can be open yet. HIE-3.1 replaces this with a Sales adapter.</summary>
public sealed class NoOrdersYetClosureGuard : ISessionClosureGuard
{
    public Task<bool> HasOpenOrdersAsync(long sessionId, CancellationToken ct) => Task.FromResult(false);
}
