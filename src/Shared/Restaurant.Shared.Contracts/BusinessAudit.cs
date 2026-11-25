namespace Restaurant.Shared.Contracts;

/// <summary>Safe business metadata only. Never include credentials, tokens or raw request payloads.</summary>
public sealed record BusinessAuditEntry(string SourceModule, string Action, string EntityType, string EntityId,
    Guid? ActorId = null, Guid? BusinessDayId = null, string? Reason = null, bool ResolveActor = true);

/// <summary>
/// Writes Audit using the source module's active transaction in the composition root. Call after business SaveChanges
/// and before commit. Failure must abort the business operation; replay paths must not call this writer again.
/// </summary>
public interface IBusinessAudit
{
    Task AppendAsync(BusinessAuditEntry entry, CancellationToken cancellationToken);
}
