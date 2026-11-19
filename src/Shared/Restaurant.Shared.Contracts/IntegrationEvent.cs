namespace Restaurant.Shared.Contracts;

public sealed record IntegrationEvent<TPayload> where TPayload : notnull
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public string Type { get; }
    public int Version { get; }
    public TPayload Payload { get; }

    public IntegrationEvent(Guid eventId, DateTimeOffset occurredAt, string type, int version, TPayload payload)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("Event ID is required.", nameof(eventId));
        if (occurredAt == default || occurredAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("A UTC occurrence time is required.", nameof(occurredAt));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        ContractNames.Validate(type, nameof(type));
        ArgumentNullException.ThrowIfNull(payload);
        EventId = eventId;
        OccurredAt = occurredAt;
        Type = type;
        Version = version;
        Payload = payload;
    }
}

/// <summary>
/// Stages an event in the caller's unit of work. Implementations must persist it atomically
/// with business changes: the event exists if and only if the surrounding transaction commits.
/// This does not publish; a dispatcher delivers it after commit (at-least-once).
/// </summary>
public interface IOutboxWriter
{
    /// <param name="aggregateId">Stable id of the aggregate that produced the event (for example the batch id).</param>
    /// <param name="aggregateVersion">Version of that aggregate when the event was produced. With the event type and
    /// aggregate id it is unique, so staging the same fact twice is rejected instead of duplicated.</param>
    Task EnqueueAsync<TPayload>(IntegrationEvent<TPayload> message, string aggregateId, int aggregateVersion,
        CancellationToken cancellationToken)
        where TPayload : notnull;
}
