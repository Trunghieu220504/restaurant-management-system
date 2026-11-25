using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Modules.Fulfillment.Infrastructure.Messaging;

namespace Restaurant.Modules.Fulfillment.Infrastructure;

public sealed class FulfillmentService(FulfillmentDbContext db, TimeProvider clock) : IFulfillmentService
{
    public async Task<IReadOnlyList<TicketView>> QueueAsync(string station, CancellationToken ct)
    {
        return await db.Tickets.AsNoTracking()
            .Where(x => x.TramChot == station && x.TrangThai != TicketStatus.Served && x.TrangThai != TicketStatus.Cancelled && x.TrangThai != TicketStatus.Failed)
            .OrderBy(x => x.TaoLuc).ThenBy(x => x.Id)
            .Select(x => ToView(x))
            .ToListAsync(ct);
    }

    public Task<FulfillmentResult<TicketView>> StartAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct) =>
        TransitionAsync("BAT_DAU", id, station, version, requestKey, actor, (ticket, now) => ticket.Start(actor, now), ct);

    public Task<FulfillmentResult<TicketView>> ReadyAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct) =>
        TransitionAsync("SAN_SANG", id, station, version, requestKey, actor, (ticket, now) => ticket.MarkReady(actor, now), ct);

    public Task<FulfillmentResult<TicketView>> DeliverAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct) =>
        TransitionAsync("GIAO_MON", id, station, version, requestKey, actor, (ticket, now) => ticket.Deliver(actor, now), ct);

    public Task<FulfillmentResult<TicketView>> CancelAsync(long id, string station, string version, Guid requestKey, Guid actor, CancellationToken ct) =>
        TransitionAsync("HUY_MON", id, station, version, requestKey, actor, (ticket, now) => ticket.Cancel(actor, now), ct);

    private async Task<FulfillmentResult<TicketView>> TransitionAsync(string operation, long id, string station, string version, Guid requestKey, Guid actor,
        Func<PhieuCheBien, DateTime, FulfillmentResult<PhieuCheBien>> transition, CancellationToken ct)
    {
        if (id <= 0 || actor == Guid.Empty || requestKey == Guid.Empty || string.IsNullOrWhiteSpace(version) ||
            !TryDecode(version, out var expectedVersion))
            return FulfillmentResult<TicketView>.Fail(FulfillmentErrors.InputInvalid);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{operation}|{id}|{station}|{version}"));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var prior = await db.Requests.AsNoTracking().SingleOrDefaultAsync(
                    x => x.Operation == operation && x.ActorScope == actor && x.Key == requestKey, ct);
                if (prior is not null)
                {
                    if (!prior.RequestHash.AsSpan().SequenceEqual(hash))
                        return FulfillmentResult<TicketView>.Fail(FulfillmentErrors.IdempotencyConflict);
                    var replay = await db.Tickets.AsNoTracking().SingleAsync(x => x.Id == prior.TicketId, ct);
                    await tx.RollbackAsync(ct);
                    return FulfillmentResult<TicketView>.Ok(ToView(replay));
                }

                var ticket = await db.Tickets.FromSqlInterpolated(
                    $"SELECT * FROM fulfillment.PhieuCheBien WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);
                if (ticket is null) return FulfillmentResult<TicketView>.Fail(FulfillmentErrors.TicketNotFound);
                if (!Stations.All.Contains(station) || ticket.TramChot != station)
                    return FulfillmentResult<TicketView>.Fail(FulfillmentErrors.StationForbidden);
                if (await db.Incidents.AnyAsync(x => x.PhieuCheBienId == id && x.TrangThai == IncidentStatus.Pending, ct))
                    return FulfillmentResult<TicketView>.Fail(FulfillmentErrors.IncidentPending);
                db.Entry(ticket).Property(x => x.Version).OriginalValue = expectedVersion;
                var result = transition(ticket, clock.GetUtcNow().UtcDateTime);
                if (!result.Succeeded) return FulfillmentResult<TicketView>.Fail(result.Error!);
                await db.SaveChangesAsync(ct);
                TicketEvents.Stage(db, ticket, clock.GetUtcNow());
                db.Requests.Add(new YeuCauFulfillmentDaXuLy
                {
                    Operation = operation,
                    ActorScope = actor,
                    Key = requestKey,
                    RequestHash = hash,
                    TicketId = ticket.Id,
                    CreatedAt = clock.GetUtcNow().UtcDateTime
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return FulfillmentResult<TicketView>.Ok(ToView(ticket));
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                return FulfillmentResult<TicketView>.Fail(FulfillmentErrors.ConcurrencyConflict);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                await tx.RollbackAsync(ct);
                continue;
            }
        }
        return FulfillmentResult<TicketView>.Fail(FulfillmentErrors.ConcurrencyConflict);
    }

    private static bool TryDecode(string value, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { bytes = []; return false; }
    }

    private static TicketView ToView(PhieuCheBien x) => new(x.Id, x.ChiTietDonId, x.TramChot, x.TrangThai, x.TaoLuc,
        x.NguoiBatDauId, x.BatDauLuc, x.NguoiHoanThanhId, x.HoanThanhLuc, x.NguoiGiaoId, x.GiaoLuc,
        Convert.ToBase64String(x.Version));
}
