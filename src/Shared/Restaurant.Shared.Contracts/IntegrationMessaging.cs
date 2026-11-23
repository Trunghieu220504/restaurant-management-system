using System.Text.Json;

namespace Restaurant.Shared.Contracts;

/// <summary>
/// An event as stored in a module Outbox and handed to consumers. <see cref="Payload"/> is the JSON
/// owned by the producing module; <see cref="Attempts"/> counts deliveries including the current one.
/// </summary>
public sealed record StoredIntegrationEvent(
    Guid EventId,
    string AggregateId,
    int AggregateVersion,
    string Type,
    int SchemaVersion,
    string Payload,
    DateTimeOffset OccurredAt,
    int Attempts);

/// <summary>
/// Consumes events of one type. Delivery is at-least-once, so <see cref="HandleAsync"/> must be idempotent:
/// record the event in the consumer's own Inbox in the same transaction as the domain effect and treat a
/// repeated <see cref="StoredIntegrationEvent.EventId"/> as already done. Throwing makes the dispatcher retry
/// with backoff; returning normally means the event is fully handled.
/// </summary>
public interface IIntegrationEventHandler
{
    /// <summary>Lowercase dotted event type this handler consumes, for example <c>sales.batch_confirmed</c>.</summary>
    string EventType { get; }

    /// <summary>Stable name of this consumer, the first half of the Inbox key (for example <c>fulfillment.tickets</c>).</summary>
    string Consumer { get; }

    Task HandleAsync(StoredIntegrationEvent message, CancellationToken cancellationToken);
}

public static class IntegrationEventJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string SerializePayload<TPayload>(TPayload payload) where TPayload : notnull =>
        JsonSerializer.Serialize(payload, Options);

    /// <summary>Rebuilds the typed envelope; the consumer picks the payload type that matches <c>Type</c> and <c>SchemaVersion</c>.</summary>
    public static IntegrationEvent<TPayload> Read<TPayload>(StoredIntegrationEvent stored) where TPayload : notnull
    {
        ArgumentNullException.ThrowIfNull(stored);
        var payload = JsonSerializer.Deserialize<TPayload>(stored.Payload, Options)
            ?? throw new JsonException("The stored payload is empty.");
        return new IntegrationEvent<TPayload>(stored.EventId, stored.OccurredAt, stored.Type, stored.SchemaVersion, payload);
    }
}

public static class OutboxRules
{
    public const int MaxAggregateIdLength = 64;

    /// <summary>Aggregate ids are short ASCII tokens so they stay indexable and safe to log.</summary>
    public static void ValidateAggregate(string aggregateId, int aggregateVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        if (aggregateId.Length > MaxAggregateIdLength ||
            aggregateId.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.' or ':')))
            throw new ArgumentException("Use up to 64 ASCII letters, digits and - _ . :", nameof(aggregateId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aggregateVersion);
    }
}
