namespace Restaurant.Modules.Audit.Contracts;

/// <summary>A proposed immutable audit message; never pass credentials, tokens or raw HTTP/SQL bodies.</summary>
public sealed record AuditEntry(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorId,
    string SourceModule,
    string Action,
    string EntityType,
    string EntityId,
    Guid? BusinessDayId,
    string? Reason = null);
