using System.Data;
using System.Data.Common;

namespace Restaurant.Shared.SqlServer;

/// <summary>
/// Consumer-side duplicate guard over <c>[schema].[Inbox]</c> with key (Consumer, EventId). Call it inside the same
/// transaction as the domain effect: if that transaction rolls back the marker rolls back with it, so the
/// event is retried; if it commits, a second delivery of the same event is recognised and skipped.
/// </summary>
public static class InboxStore
{
    /// <returns>True when this is the first time the consumer sees the event (do the work); false for a repeat (skip it).</returns>
    public static async Task<bool> TryRecordAsync(DbConnection connection, DbTransaction transaction, string schema,
        string consumer, Guid eventId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        MessagingSql.RequireOpen(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("An active transaction on the supplied connection is required.", nameof(transaction));
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);
        if (consumer.Length > 100) throw new ArgumentException("Consumer names are at most 100 characters.", nameof(consumer));
        if (eventId == Guid.Empty) throw new ArgumentException("Event ID is required.", nameof(eventId));
        var table = MessagingSql.Table(schema, "Inbox");

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // UPDLOCK + HOLDLOCK takes a key-range lock: two deliveries of one event serialize here, and the
        // second one sees the committed marker instead of racing to insert it.
        command.CommandText = $"""
            INSERT INTO {table} ([Consumer], [EventId], [ProcessedAt])
            SELECT @consumer, @eventId, @now
            WHERE NOT EXISTS (
                SELECT 1 FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE [Consumer] = @consumer AND [EventId] = @eventId);
            """;
        MessagingSql.AddParameter(command, "@consumer", DbType.AnsiString, consumer);
        MessagingSql.AddParameter(command, "@eventId", DbType.Guid, eventId);
        MessagingSql.AddParameter(command, "@now", DbType.DateTime2, MessagingSql.Utc(now));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
