using Microsoft.Data.SqlClient;
using Restaurant.Shared.Contracts;
using Restaurant.Shared.SqlServer;

namespace Restaurant.Api.Modules;

/// <summary>
/// Runs the Outbox dispatcher for every module that owns an Outbox table. Off unless <c>Outbox:Enabled</c> is true,
/// so nothing is delivered until a consumer exists. A message whose type has no registered handler is a delivery
/// failure (retried, then parked), never silently dropped; handlers must be idempotent because delivery is at-least-once.
/// </summary>
public sealed class OutboxDispatcherService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    /// <summary>Schemas that have an Outbox table. Add a module here when its migration creates the table.</summary>
    private static readonly string[] Schemas = ["sales", "salesday"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Outbox:Enabled", false)) return;
        var connectionString = configuration.GetConnectionString("Restaurant");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var pollSeconds = Math.Clamp(configuration.GetValue("Outbox:PollSeconds", 2), 1, 300);

        var options = new OutboxDispatcherOptions();
        var dispatchers = Schemas.Select(schema => new OutboxDispatcher(async ct =>
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            return connection;
        }, schema, DeliverAsync, options, clock, message => logger.LogWarning(
            "Outbox message {EventId} ({Type}) was completed by another dispatcher while this one was delivering it; " +
            "delivery took longer than the {Lease} lease.", message.EventId, message.Type, options.LeaseDuration))).ToArray();

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollSeconds));
        do
        {
            foreach (var dispatcher in dispatchers)
            {
                try
                {
                    // Drain while batches are full so a backlog does not wait one poll per batch.
                    while (await dispatcher.RunOnceAsync(stoppingToken) >= options.BatchSize) { }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outbox dispatch cycle failed; it will be retried on the next poll.");
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var stored = new StoredIntegrationEvent(message.EventId, message.AggregateId, message.AggregateVersion,
            message.Type, message.SchemaVersion, message.Payload, message.OccurredAt, message.Attempts);
        await using var scope = scopes.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler>()
            .Where(h => string.Equals(h.EventType, message.Type, StringComparison.Ordinal)).ToArray();
        if (handlers.Length == 0)
        {
            // Never drop an event nobody consumed yet: fail so it backs off, parks with this error, and can be
            // requeued once the consumer is deployed.
            throw new InvalidOperationException($"No handler is registered for event type '{message.Type}'.");
        }
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(stored, cancellationToken);
        }
    }
}
