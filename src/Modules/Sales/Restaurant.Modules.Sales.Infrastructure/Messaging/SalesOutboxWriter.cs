using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;

namespace Restaurant.Modules.Sales.Infrastructure.Messaging;

/// <summary>
/// Stages Sales integration events in the current <see cref="SalesDbContext"/>. Nothing is sent here: the row is
/// saved by the caller's SaveChanges (or rolled back with it) and delivered later by the dispatcher.
/// </summary>
internal sealed class SalesOutboxWriter(SalesDbContext db) : IOutboxWriter
{
    public Task EnqueueAsync<TPayload>(IntegrationEvent<TPayload> message, string aggregateId, int aggregateVersion,
        CancellationToken cancellationToken)
        where TPayload : notnull
    {
        ArgumentNullException.ThrowIfNull(message);
        OutboxRules.ValidateAggregate(aggregateId, aggregateVersion);
        db.Set<OutboxEntry>().Add(new OutboxEntry
        {
            Id = message.EventId,
            AggregateId = aggregateId,
            AggregateVersion = aggregateVersion,
            Type = message.Type,
            SchemaVersion = message.Version,
            Payload = IntegrationEventJson.SerializePayload(message.Payload),
            OccurredAt = message.OccurredAt.UtcDateTime,
            NextAttemptAt = message.OccurredAt.UtcDateTime
        });
        return Task.CompletedTask;
    }
}
