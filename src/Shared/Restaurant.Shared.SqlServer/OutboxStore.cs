using System.Data;
using System.Data.Common;

namespace Restaurant.Shared.SqlServer;

/// <summary>
/// Dispatcher-side operations on <c>[schema].[Outbox]</c>. Rows are written by the producing module inside its own
/// business transaction (see the module outbox writer), so a row is visible here only after that transaction commits.
/// None of these methods open a transaction: each statement commits on its own, on a connection the caller opened.
/// </summary>
public static class OutboxStore
{
    private const int MaxErrorLength = 1000;

    /// <summary>
    /// Leases up to <paramref name="batchSize"/> due messages for <paramref name="lease"/> and counts the attempt.
    /// Rows locked by another dispatcher are skipped (READPAST), so several instances never take the same message
    /// at the same time; a lease that expires makes the message due again. Messages come back ordered by occurrence
    /// time; this is not a per-aggregate ordering guarantee across retries.
    /// </summary>
    public static async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(DbConnection connection, string schema,
        DateTimeOffset now, int batchSize, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        MessagingSql.RequireOpen(connection);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, 500);
        if (lease <= TimeSpan.Zero || lease > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(lease), "Use a lease between zero and one hour.");
        var table = MessagingSql.Table(schema, "Outbox");

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH due AS (
                SELECT TOP (@batch) *
                FROM {table} WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [ProcessedAt] IS NULL AND [NextAttemptAt] IS NOT NULL AND [NextAttemptAt] <= @now
                  AND ([LeaseUntil] IS NULL OR [LeaseUntil] <= @now)
                ORDER BY [NextAttemptAt], [Id])
            UPDATE due SET [LeaseUntil] = @leaseUntil, [Attempts] = [Attempts] + 1
            OUTPUT inserted.[Id], inserted.[AggregateId], inserted.[AggregateVersion], inserted.[Type],
                   inserted.[SchemaVersion], inserted.[Payload], inserted.[OccurredAt], inserted.[Attempts];
            """;
        MessagingSql.AddParameter(command, "@batch", DbType.Int32, batchSize);
        MessagingSql.AddParameter(command, "@now", DbType.DateTime2, MessagingSql.Utc(now));
        MessagingSql.AddParameter(command, "@leaseUntil", DbType.DateTime2, MessagingSql.Utc(now + lease));

        var claimed = new List<OutboxMessage>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                claimed.Add(new OutboxMessage(
                    reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.GetInt32(4),
                    reader.GetString(5), MessagingSql.FromUtc(reader.GetDateTime(6)), reader.GetInt32(7)));
            }
        }
        return claimed.OrderBy(x => x.OccurredAt).ThenBy(x => x.EventId).ToArray();
    }

    /// <returns>False when the message was already completed (another dispatcher finished it first).</returns>
    public static async Task<bool> MarkProcessedAsync(DbConnection connection, string schema, Guid eventId,
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        MessagingSql.RequireOpen(connection);
        var table = MessagingSql.Table(schema, "Outbox");
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {table} SET [ProcessedAt] = @now, [LeaseUntil] = NULL, [NextAttemptAt] = NULL, [LastError] = NULL
            WHERE [Id] = @id AND [ProcessedAt] IS NULL;
            """;
        MessagingSql.AddParameter(command, "@id", DbType.Guid, eventId);
        MessagingSql.AddParameter(command, "@now", DbType.DateTime2, MessagingSql.Utc(now));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    /// <summary>
    /// Releases the lease after a failed delivery. A <paramref name="retryAt"/> schedules the next attempt; null parks the
    /// message (it keeps its error and is no longer picked up until <see cref="RequeueParkedAsync"/>).
    /// </summary>
    public static async Task<bool> MarkFailedAsync(DbConnection connection, string schema, Guid eventId, string error,
        DateTimeOffset? retryAt, CancellationToken cancellationToken = default)
    {
        MessagingSql.RequireOpen(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        var table = MessagingSql.Table(schema, "Outbox");
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {table} SET [LeaseUntil] = NULL, [NextAttemptAt] = @retryAt, [LastError] = @error
            WHERE [Id] = @id AND [ProcessedAt] IS NULL;
            """;
        MessagingSql.AddParameter(command, "@id", DbType.Guid, eventId);
        MessagingSql.AddParameter(command, "@retryAt", DbType.DateTime2, retryAt is null ? null : MessagingSql.Utc(retryAt.Value));
        MessagingSql.AddParameter(command, "@error", DbType.String, error.Length > MaxErrorLength ? error[..MaxErrorLength] : error);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    /// <summary>Makes parked messages due again with a fresh attempt count, for an operator who fixed the cause.</summary>
    public static async Task<int> RequeueParkedAsync(DbConnection connection, string schema, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        MessagingSql.RequireOpen(connection);
        var table = MessagingSql.Table(schema, "Outbox");
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {table} SET [NextAttemptAt] = @now, [Attempts] = 0, [LeaseUntil] = NULL
            WHERE [ProcessedAt] IS NULL AND [NextAttemptAt] IS NULL;
            """;
        MessagingSql.AddParameter(command, "@now", DbType.DateTime2, MessagingSql.Utc(now));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
