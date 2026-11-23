namespace Restaurant.Modules.Audit.Contracts;

/// <summary>
/// Append-only sink for audit messages. Producers require a separately reviewed durable
/// transaction boundary; this sink alone does not make their business operations atomic.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Persists a message idempotently by EventId. Delivery of an identical message twice must not
    /// duplicate it. A conflicting payload for the same EventId must fail rather than overwrite.
    /// This interface alone does not make a producer's business transaction atomic with Audit.
    /// </summary>
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
