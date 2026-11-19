using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Audit.Application;
using Restaurant.Modules.Audit.Contracts;
using Restaurant.Modules.Audit.Domain;
using Restaurant.Modules.Audit.Infrastructure.Persistence;

namespace Restaurant.Modules.Audit.Infrastructure;

/// <summary>Idempotent sink only; producers still require their reviewed durable transaction boundary.</summary>
public sealed class AuditLog(AuditDbContext db, TimeProvider clock) : IAuditLog
{
    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var record = NhatKy.Create(entry.EventId, entry.OccurredAtUtc, clock.GetUtcNow().ToUniversalTime(),
            entry.ActorId, entry.SourceModule, entry.Action, entry.EntityType, entry.EntityId,
            entry.BusinessDayId, entry.Reason);
        var existing = await db.Entries.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == entry.EventId, cancellationToken);
        if (existing is not null) { RequireSame(existing, entry); return; }

        db.Entries.Add(record);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(record).State = EntityState.Detached;
            existing = await db.Entries.AsNoTracking().SingleAsync(x => x.EventId == entry.EventId, cancellationToken);
            RequireSame(existing, entry);
        }
    }

    private static void RequireSame(NhatKy existing, AuditEntry input)
    {
        var stored = new AuditEntry(existing.EventId, existing.OccurredAtUtc, existing.ActorId,
            existing.SourceModule, existing.Action, existing.EntityType, existing.EntityId,
            existing.BusinessDayId, existing.Reason);
        if (stored != input) throw new AuditEventConflictException();
    }
}

public sealed class AuditQueries(AuditDbContext db) : IAuditQueries
{
    public async Task<AuditLogPage> FindAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.IsValid) throw new ArgumentException("Invalid audit query.", nameof(query));
        var rows = db.Entries.AsNoTracking();
        if (query.ActorId is { } actor) rows = rows.Where(x => x.ActorId == actor);
        if (query.EntityType is { } type) rows = rows.Where(x => x.EntityType == type);
        if (query.EntityId is { } id) rows = rows.Where(x => x.EntityId == id);
        if (query.FromUtc is { } from) rows = rows.Where(x => x.OccurredAtUtc >= from);
        if (query.ToUtc is { } to) rows = rows.Where(x => x.OccurredAtUtc < to);
        var total = await rows.LongCountAsync(cancellationToken);
        var items = await rows.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.EventId)
            .Skip((int)(((long)query.Page - 1) * query.PageSize)).Take(query.PageSize)
            .Select(x => new AuditLogItem(x.EventId, x.OccurredAtUtc, x.RecordedAtUtc, x.ActorId,
                x.SourceModule, x.Action, x.EntityType, x.EntityId, x.BusinessDayId, x.Reason))
            .ToArrayAsync(cancellationToken);
        return new AuditLogPage(items, query.Page, query.PageSize, total);
    }
}
