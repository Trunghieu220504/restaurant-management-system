namespace Restaurant.Modules.Audit.Contracts;

/// <summary>
/// Proposed append-only sink for audit messages. The implementation and producer transaction
/// integration require owner review before registration or use by another module.
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
