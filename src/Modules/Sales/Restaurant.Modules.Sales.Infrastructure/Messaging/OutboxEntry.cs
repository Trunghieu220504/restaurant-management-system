namespace Restaurant.Modules.Sales.Infrastructure.Messaging;

/// <summary>
/// Technical Outbox row (not a domain entity). It is written in the same SaveChanges as the business change, so the
/// event exists if and only if that change commits. The dispatcher in Restaurant.Shared.SqlServer owns the delivery columns.
/// </summary>
internal sealed class OutboxEntry
{
    public Guid Id { get; set; }
    public string AggregateId { get; set; } = string.Empty;
    public int AggregateVersion { get; set; }
    public string Type { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    /// <summary>Null means parked: the dispatcher gave up and an operator must requeue it.</summary>
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}
