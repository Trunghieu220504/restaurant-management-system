using Restaurant.Modules.Audit.Domain;

namespace Restaurant.Modules.Audit.Application;

/// <summary>Validated inclusive/exclusive UTC filters and bounded pagination for audit reads.</summary>
public sealed record AuditLogQuery(Guid? ActorId = null, string? EntityType = null, string? EntityId = null,
    DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null, int Page = 1, int PageSize = 20)
{
    public bool IsValid => ActorId != Guid.Empty &&
        (EntityType is null || NhatKy.ValidText(EntityType, 100)) &&
        (EntityId is null || NhatKy.ValidText(EntityId, 100)) &&
        (FromUtc is null || (FromUtc != default(DateTimeOffset) && FromUtc.Value.Offset == TimeSpan.Zero)) &&
        (ToUtc is null || (ToUtc != default(DateTimeOffset) && ToUtc.Value.Offset == TimeSpan.Zero)) &&
        (FromUtc is null || ToUtc is null || FromUtc < ToUtc) &&
        Page >= 1 && PageSize is >= 1 and <= 100 && ((long)Page - 1) * PageSize <= int.MaxValue;
}

/// <summary>A bounded audit record; contains no authentication or raw request payload.</summary>
public sealed record AuditLogItem(Guid EventId, DateTimeOffset OccurredAtUtc, DateTimeOffset RecordedAtUtc,
    Guid? ActorId, string SourceModule, string Action, string EntityType, string EntityId,
    Guid? BusinessDayId, string? Reason);

/// <summary>A stable page ordered by occurrence time and event ID descending.</summary>
public sealed record AuditLogPage(IReadOnlyList<AuditLogItem> Items, int Page, int PageSize, long Total);

public interface IAuditQueries
{
    Task<AuditLogPage> FindAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The same event ID was delivered with a different business payload.</summary>
public sealed class AuditEventConflictException() : InvalidOperationException("Conflicting audit event ID.");
