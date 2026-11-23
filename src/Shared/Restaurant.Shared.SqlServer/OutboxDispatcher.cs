using System.Data.Common;

namespace Restaurant.Shared.SqlServer;

public sealed record OutboxDispatcherOptions
{
    public int BatchSize { get; init; } = 20;

    /// <summary>How long a claimed message is invisible to other dispatchers. Must exceed the slowest delivery.</summary>
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>After this many attempts the message is parked with its last error instead of retrying forever.</summary>
    public int MaxAttempts { get; init; } = 8;

    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Delivers committed Outbox messages of one module schema. Delivery is at-least-once: a crash between delivery and
/// completion, or an expired lease, delivers the message again, so consumers must deduplicate by event id (Inbox).
/// The delivery callback runs outside any database transaction of this class.
/// </summary>
public sealed class OutboxDispatcher(
    Func<CancellationToken, Task<DbConnection>> openConnection,
    string schema,
    Func<OutboxMessage, CancellationToken, Task> deliver,
    OutboxDispatcherOptions options,
    TimeProvider clock,
    Action<OutboxMessage>? completedElsewhere = null)
{
    /// <summary>Claims and delivers one batch. Returns how many messages were claimed.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var connection = await openConnection(cancellationToken);
        var claimed = await OutboxStore.ClaimDueAsync(connection, schema, clock.GetUtcNow(), options.BatchSize,
            options.LeaseDuration, cancellationToken);
        foreach (var message in claimed)
        {
            try
            {
                await deliver(message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // Shutdown: leave the lease to expire; the message is delivered again later.
            }
            catch (Exception ex)
            {
                DateTimeOffset? retryAt = message.Attempts >= options.MaxAttempts
                    ? null
                    : clock.GetUtcNow() + Backoff(message.Attempts);
                await OutboxStore.MarkFailedAsync(connection, schema, message.EventId,
                    ex.GetType().Name + ": " + ex.Message, retryAt, cancellationToken);
                continue;
            }
            // False means the lease expired during delivery and another dispatcher already completed the message: it was
            // delivered twice. Harmless for an idempotent consumer, but the caller should know the lease is too short.
            if (!await OutboxStore.MarkProcessedAsync(connection, schema, message.EventId, clock.GetUtcNow(), cancellationToken))
                completedElsewhere?.Invoke(message);
        }
        return claimed.Count;
    }

    private TimeSpan Backoff(int attempts)
    {
        var factor = Math.Pow(2, Math.Min(attempts - 1, 20));
        var ticks = (long)Math.Min(options.BaseDelay.Ticks * factor, options.MaxDelay.Ticks);
        var delay = TimeSpan.FromTicks(ticks);
        return delay < options.BaseDelay ? options.BaseDelay : delay;
    }
}
