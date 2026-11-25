using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Fulfillment.Infrastructure.Messaging;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;

namespace Restaurant.Modules.Fulfillment.Infrastructure;

public sealed class FulfillmentIncidents(FulfillmentDbContext db, IFulfillmentIncidentContext context, TimeProvider clock) : IFulfillmentIncidents
{
    public async Task<IReadOnlyList<IncidentView>> PendingAsync(IReadOnlyCollection<string> stations, CancellationToken ct)
    {
        var rows = await (from incident in db.Incidents.AsNoTracking()
                          join ticket in db.Tickets on incident.PhieuCheBienId equals ticket.Id
                          where incident.TrangThai == IncidentStatus.Pending && stations.Contains(ticket.TramChot)
                          orderby incident.BaoLuc, incident.Id
                          select incident).ToArrayAsync(ct);
        return rows.Select(x => View(x, null)).ToArray();
    }

    public async Task<FulfillmentResult<IncidentView>> ReportAsync(long ticketId, IncidentReport request, Guid actor, CancellationToken ct)
    {
        var report = SuCoMon.Report(ticketId, request.Reason, actor, request.RequestKey, clock.GetUtcNow().UtcDateTime);
        if (!report.Succeeded || !ValidVersion(request.Version) || !Stations.All.Contains(request.Station))
            return Fail(FulfillmentErrors.InputInvalid);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { ticketId, request }));
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Serialize a report key independently of the ticket so concurrent retries on different tickets cannot race.
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"fulfillment:report:" + request.RequestKey}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51001, 'Incident lock unavailable', 1;", ct);
            var prior = await db.Incidents.SingleOrDefaultAsync(x => x.KhoaYeuCau == request.RequestKey, ct);
            if (prior is not null)
            {
                var saved = await db.Requests.AsNoTracking().SingleOrDefaultAsync(x => x.Operation == "INCIDENT_REPORT" &&
                    x.ActorScope == actor && x.Key == request.RequestKey, ct);
                return saved is not null && saved.RequestHash.AsSpan().SequenceEqual(hash)
                    ? FulfillmentResult<IncidentView>.Ok(await ViewAsync(prior, ct)) : Fail(FulfillmentErrors.IdempotencyConflict);
            }
            var reference = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ticketId, ct);
            if (reference is null) return Fail(FulfillmentErrors.TicketNotFound);
            var error = await context.RequireOpenAsync(tx.GetDbTransaction(), reference.ChiTietDonId, ct);
            if (error is not null) return Fail(error);
            var ticket = await LockTicket(ticketId, ct);
            if (ticket.TramChot != request.Station) return Fail(FulfillmentErrors.StationForbidden);
            if (Convert.ToBase64String(ticket.Version) != request.Version) return Fail(FulfillmentErrors.ConcurrencyConflict);
            if (ticket.TrangThai is TicketStatus.Cancelled or TicketStatus.Failed ||
                await db.Tickets.AnyAsync(x => x.PhieuGocId == ticketId, ct)) return Fail(FulfillmentErrors.AlreadyProcessed);
            if (await db.Incidents.AnyAsync(x => x.PhieuCheBienId == ticketId && x.TrangThai == IncidentStatus.Pending, ct))
                return Fail(FulfillmentErrors.IncidentPending);
            db.Incidents.Add(report.Value!);
            db.Requests.Add(new YeuCauFulfillmentDaXuLy
            {
                Operation = "INCIDENT_REPORT",
                ActorScope = actor,
                Key = request.RequestKey,
                RequestHash = hash,
                TicketId = ticketId,
                CreatedAt = clock.GetUtcNow().UtcDateTime
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return FulfillmentResult<IncidentView>.Ok(View(report.Value!, null));
        }
        catch (DbUpdateConcurrencyException) { return Fail(FulfillmentErrors.ConcurrencyConflict); }
    }

    public async Task<FulfillmentResult<IncidentView>> ResolveAsync(long incidentId, IncidentResolution request, Guid actor, CancellationToken ct)
    {
        if (incidentId <= 0 || actor == Guid.Empty || request.RequestKey == Guid.Empty || !ValidVersion(request.Version) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000 ||
            request.Decision is not null && !IncidentDecision.All.Contains(request.Decision)) return Fail(FulfillmentErrors.InputInvalid);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { incidentId, request }));
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"fulfillment:decide:" + actor + ":" + request.RequestKey}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51001, 'Incident lock unavailable', 1;", ct);
        var incidentReference = await db.Incidents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == incidentId, ct);
        if (incidentReference is null) return Fail(FulfillmentErrors.IncidentNotFound);
        var reference = await db.Tickets.AsNoTracking().SingleAsync(x => x.Id == incidentReference.PhieuCheBienId, ct);
        const string operation = "INCIDENT_DECIDE";
        var prior = await db.Requests.AsNoTracking().SingleOrDefaultAsync(x => x.Operation == operation && x.ActorScope == actor && x.Key == request.RequestKey, ct);
        if (prior is not null) return prior.RequestHash.AsSpan().SequenceEqual(hash)
            ? FulfillmentResult<IncidentView>.Ok(await ViewAsync(incidentReference, ct)) : Fail(FulfillmentErrors.IdempotencyConflict);
        var error = await context.RequireOpenAsync(tx.GetDbTransaction(), reference.ChiTietDonId, ct);
        if (error is not null) return Fail(error);
        var ticket = await LockTicket(reference.Id, ct);
        // Re-read after the ticket lock; all reports, decisions and ticket commands share this serialization point.
        var incident = await db.Incidents.SingleAsync(x => x.Id == incidentId, ct);
        prior = await db.Requests.AsNoTracking().SingleOrDefaultAsync(x => x.Operation == operation && x.ActorScope == actor && x.Key == request.RequestKey, ct);
        if (prior is not null) return prior.RequestHash.AsSpan().SequenceEqual(hash)
            ? FulfillmentResult<IncidentView>.Ok(await ViewAsync(incident, ct)) : Fail(FulfillmentErrors.IdempotencyConflict);
        if (Convert.ToBase64String(incident.Version) != request.Version) return Fail(FulfillmentErrors.ConcurrencyConflict);
        var decision = incident.Decide(request.Decision, request.Reason, actor, clock.GetUtcNow().UtcDateTime);
        if (!decision.Succeeded) return Fail(decision.Error!);
        PhieuCheBien? remake = null;
        if (request.Decision is not null)
        {
            var resolved = ticket.ResolveIncident(incident, actor, clock.GetUtcNow().UtcDateTime);
            if (!resolved.Succeeded) return Fail(resolved.Error!);
            if (request.Decision == IncidentDecision.Remake) { remake = resolved.Value!; db.Tickets.Add(remake); }
            if (request.Decision == IncidentDecision.CancelWithObligationAdjustment)
            {
                error = await context.ReduceObligationAsync(tx.GetDbTransaction(), ticket.ChiTietDonId, incident.Id,
                    actor, request.RequestKey, request.Reason.Trim(), ct);
                if (error is not null) return Fail(error);
            }
        }
        db.Requests.Add(new YeuCauFulfillmentDaXuLy
        {
            Operation = operation,
            ActorScope = actor,
            Key = request.RequestKey,
            RequestHash = hash,
            TicketId = ticket.Id,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        });
        try
        {
            await db.SaveChangesAsync(ct);
            if (request.Decision is not null) TicketEvents.Stage(db, ticket, clock.GetUtcNow());
            if (remake is not null) TicketEvents.Stage(db, remake, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return FulfillmentResult<IncidentView>.Ok(View(incident, remake?.Id));
        }
        catch (DbUpdateConcurrencyException) { return Fail(FulfillmentErrors.ConcurrencyConflict); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return Fail(FulfillmentErrors.IdempotencyConflict); }
    }

    private Task<PhieuCheBien> LockTicket(long id, CancellationToken ct) => db.Tickets.FromSqlInterpolated(
        $"SELECT * FROM fulfillment.PhieuCheBien WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleAsync(ct);
    private async Task<IncidentView> ViewAsync(SuCoMon incident, CancellationToken ct) => View(incident,
        await db.Tickets.Where(x => x.SuCoNguonId == incident.Id).Select(x => (long?)x.Id).SingleOrDefaultAsync(ct));
    private static IncidentView View(SuCoMon x, long? remake) => new(x.Id, x.PhieuCheBienId, x.LyDo, x.TrangThai,
        x.QuyetDinh, x.NguoiBaoId, x.BaoLuc, x.NguoiDuyetId, x.DuyetLuc, x.LyDoDuyet, Convert.ToBase64String(x.Version), remake);
    private static FulfillmentResult<IncidentView> Fail(string error) => FulfillmentResult<IncidentView>.Fail(error);
    private static bool ValidVersion(string? version)
    {
        if (string.IsNullOrEmpty(version)) return false;
        try { return Convert.FromBase64String(version).Length == 8; } catch (FormatException) { return false; }
    }
}
